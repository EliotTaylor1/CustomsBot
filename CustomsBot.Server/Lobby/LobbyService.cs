using System.Collections.Concurrent;
using CustomsBot.Data;
using CustomsBot.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomsBot.Server.Lobby;

/// <summary>
/// Server-authoritative lobby state. Holds in-memory state per game and validates every
/// intent; clients only send intents. Persists <see cref="GamePlayer"/> rows when a draft
/// starts. State for a game lives until the API restarts (lobbies are ephemeral pregame).
/// </summary>
public class LobbyService(IServiceScopeFactory scopeFactory)
{
    private readonly ConcurrentDictionary<Guid, LobbyState> _lobbies = new();

    public async Task<IReadOnlyList<SeriesSummaryDto>> ListSeriesAsync(IReadOnlyList<ulong> guildIds, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomsBotDbContext>();
        return await db.Series
            .Where(s => guildIds.Contains(s.GuildId))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SeriesSummaryDto(
                s.Id, s.Name, s.Status.ToString(), s.BestOf, s.Participants.Count))
            .ToListAsync(ct);
    }

    public async Task<LobbyStateDto?> CreateLobbyAsync(Guid seriesId, IReadOnlyList<ulong> guildIds, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomsBotDbContext>();

        var series = await LoadSeriesAsync(db, seriesId, ct);
        if (series is null || series.Teams.Count < 2 || !guildIds.Contains(series.GuildId))
            return null;

        var gameNumber = await db.Games.CountAsync(g => g.SeriesId == seriesId, ct) + 1;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            SeriesId = seriesId,
            GameNumber = gameNumber,
            Status = GameStatus.Lobby,
        };
        db.Games.Add(game);
        await db.SaveChangesAsync(ct);

        return ToDto(_lobbies.GetOrAdd(game.Id, BuildState(game, series)));
    }

    /// <summary>
    /// Returns lobby state, lazily building it from a persisted game in <c>lobby</c> status
    /// (e.g. one opened by the bot's <c>/series new-draft</c>). Null if not a lobby.
    /// </summary>
    public async Task<LobbyStateDto?> EnsureLobbyAsync(Guid gameId, IReadOnlyList<ulong> guildIds, CancellationToken ct = default)
    {
        if (_lobbies.TryGetValue(gameId, out var cached))
            return guildIds.Contains(cached.GuildId) ? ToDto(cached) : null;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomsBotDbContext>();

        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId && g.Status == GameStatus.Lobby, ct);
        if (game is null)
            return null;

        var series = await LoadSeriesAsync(db, game.SeriesId, ct);
        if (series is null || series.Teams.Count < 2 || !guildIds.Contains(series.GuildId))
            return null;

        return ToDto(_lobbies.GetOrAdd(game.Id, BuildState(game, series)));
    }

    private static Task<Series?> LoadSeriesAsync(CustomsBotDbContext db, Guid seriesId, CancellationToken ct) =>
        db.Series
            .Include(s => s.Teams)
            .Include(s => s.Participants).ThenInclude(p => p.Player)
            .FirstOrDefaultAsync(s => s.Id == seriesId, ct);

    private static LobbyState BuildState(Game game, Series series)
    {
        var teams = series.Teams.OrderBy(t => t.Name).ToList();
        var captainIds = teams
            .Where(t => t.CaptainPlayerId is not null)
            .Select(t => t.CaptainPlayerId!.Value)
            .ToHashSet();
        var players = series.Participants.ToDictionary(
            p => p.PlayerId,
            p => new LobbyPlayer
            {
                PlayerId = p.PlayerId,
                DiscordId = p.Player.DiscordId,
                Username = p.Player.DiscordUsername,
                Avatar = p.Player.DiscordAvatar,
                HasPuuid = p.Player.Puuid is not null,
                IsCaptain = captainIds.Contains(p.PlayerId),
            });

        return new LobbyState
        {
            GameId = game.Id,
            SeriesId = series.Id,
            GuildId = series.GuildId,
            OwnerDiscordId = series.OwnerDiscordId,
            SeriesName = series.Name,
            GameNumber = game.GameNumber,
            BlueTeamId = teams[0].Id,
            RedTeamId = teams[1].Id,
            BlueTeamName = teams[0].Name,
            RedTeamName = teams[1].Name,
            Players = players,
        };
    }

    public LobbyStateDto? GetState(Guid gameId) =>
        _lobbies.TryGetValue(gameId, out var state) ? ToDto(state) : null;

    /// <summary>Players may move themselves between sides; the series owner or a team captain may move anyone.</summary>
    public LobbyStateDto? AssignSide(Guid gameId, Guid playerId, string side, ulong callerDiscordId)
    {
        if (!TryParseSide(side, out var parsed))
            return null;
        if (!_lobbies.TryGetValue(gameId, out var state))
            return null;

        lock (state.Gate)
        {
            if (state.Started || !state.Players.TryGetValue(playerId, out var player))
                return null;
            if (player.DiscordId != callerDiscordId && !CanManage(state, callerDiscordId))
                return null;
            player.Side = parsed;
            state.Sequence++;
            return ToDto(state);
        }
    }

    public LobbyStateDto? SetRole(Guid gameId, Guid playerId, string? role, ulong callerDiscordId)
    {
        Role? parsed = null;
        if (!string.IsNullOrEmpty(role))
        {
            if (!Enum.TryParse<Role>(role, ignoreCase: true, out var r))
                return null;
            parsed = r;
        }
        return Mutate(gameId, playerId, callerDiscordId, p => p.Role = parsed);
    }

    public LobbyStateDto? SetReady(Guid gameId, Guid playerId, bool ready, ulong callerDiscordId) =>
        Mutate(gameId, playerId, callerDiscordId, p => p.IsReady = ready);

    public LobbyStateDto? SetTeamName(Guid gameId, string side, string name, ulong callerDiscordId)
    {
        if (!_lobbies.TryGetValue(gameId, out var state))
            return null;
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > 128)
            return null;

        lock (state.Gate)
        {
            if (state.Started || !CanManage(state, callerDiscordId))
                return null;
            if (string.Equals(side, "blue", StringComparison.OrdinalIgnoreCase))
                state.BlueTeamName = trimmed;
            else if (string.Equals(side, "red", StringComparison.OrdinalIgnoreCase))
                state.RedTeamName = trimmed;
            else
                return null;
            state.Sequence++;
            return ToDto(state);
        }
    }

    public async Task<LobbyStateDto?> StartAsync(Guid gameId, ulong callerDiscordId, CancellationToken ct = default)
    {
        if (!_lobbies.TryGetValue(gameId, out var state))
            return null;

        lock (state.Gate)
        {
            if (state.Started || !CanStart(state) || !CanManage(state, callerDiscordId))
                return null;
            state.Started = true;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomsBotDbContext>();

        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId, ct);
        if (game is null)
            return null;

        foreach (var p in state.Players.Values)
        {
            db.GamePlayers.Add(new GamePlayer
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                PlayerId = p.PlayerId,
                Side = p.Side,
                Role = p.Role,
                IsReady = p.IsReady,
            });
        }

        game.BlueTeamId = state.BlueTeamId;
        game.RedTeamId = state.RedTeamId;
        game.Status = GameStatus.Drafting;

        var blueTeam = await db.SeriesTeams.FindAsync([state.BlueTeamId], ct);
        if (blueTeam is not null) blueTeam.Name = state.BlueTeamName;
        var redTeam = await db.SeriesTeams.FindAsync([state.RedTeamId], ct);
        if (redTeam is not null) redTeam.Name = state.RedTeamName;

        await db.SaveChangesAsync(ct);

        lock (state.Gate)
        {
            state.Sequence++;
            return ToDto(state);
        }
    }

    /// <summary>Role/ready are self-only: the caller may only mutate the player bound to their own Discord id.</summary>
    private LobbyStateDto? Mutate(Guid gameId, Guid playerId, ulong callerDiscordId, Action<LobbyPlayer> apply)
    {
        if (!_lobbies.TryGetValue(gameId, out var state))
            return null;

        lock (state.Gate)
        {
            if (state.Started || !state.Players.TryGetValue(playerId, out var player))
                return null;
            if (player.DiscordId != callerDiscordId)
                return null;
            apply(player);
            state.Sequence++;
            return ToDto(state);
        }
    }

    private static bool CanManage(LobbyState state, ulong callerDiscordId) =>
        callerDiscordId == state.OwnerDiscordId ||
        state.Players.Values.Any(p => p.DiscordId == callerDiscordId && p.IsCaptain);

    private static bool CanStart(LobbyState state)
    {
        var nonSpectators = state.Players.Values.Where(p => p.Side != TeamSide.Spectator).ToList();
        var hasBlue = nonSpectators.Any(p => p.Side == TeamSide.Blue);
        var hasRed = nonSpectators.Any(p => p.Side == TeamSide.Red);
        return hasBlue && hasRed && nonSpectators.All(p => p.IsReady);
    }

    private static bool TryParseSide(string side, out TeamSide parsed) =>
        Enum.TryParse(side, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

    private static LobbyStateDto ToDto(LobbyState state)
    {
        var players = state.Players.Values
            .Select(p => new LobbyPlayerDto(
                p.PlayerId, p.DiscordId.ToString(), p.Username, p.Avatar, p.HasPuuid, p.IsCaptain,
                p.Side.ToString().ToLowerInvariant(), p.Role?.ToString(), p.IsReady))
            .OrderBy(p => p.Username)
            .ToList();

        return new LobbyStateDto(
            state.GameId, state.SeriesId, state.SeriesName, state.GameNumber,
            state.BlueTeamName, state.RedTeamName, state.OwnerDiscordId.ToString(), players,
            CanStart(state), state.Started, state.Sequence);
    }
}
