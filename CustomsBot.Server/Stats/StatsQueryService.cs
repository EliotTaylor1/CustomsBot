using System.Text.Json;
using CustomsBot.Data;
using CustomsBot.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomsBot.Server.Stats;

/// <summary>Read-model queries for the stats app (§5h). Promoted columns drive aggregates; the raw jsonb backs detail views.</summary>
public class StatsQueryService(CustomsBotDbContext db)
{
    public async Task<IReadOnlyList<SeriesSearchRow>> SearchSeriesAsync(
        IReadOnlyList<ulong> guildIds, string? q, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var query = db.Series.Where(s => guildIds.Contains(s.GuildId));
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(s => EF.Functions.ILike(s.Name, $"%{q}%"));
        if (from is { } f)
            query = query.Where(s => s.CreatedAt >= f);
        if (to is { } t)
            query = query.Where(s => s.CreatedAt <= t);

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SeriesSearchRow(
                s.Id, s.Name, s.Status.ToString(), s.BestOf, s.Participants.Count, s.CreatedAt))
            .Take(100)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PlayerSearchRow>> SearchPlayersAsync(
        IReadOnlyList<ulong> guildIds, string? q, CancellationToken ct = default)
    {
        // Only players who appear in a series in one of the viewer's servers.
        var query = db.Players.Where(p =>
            db.SeriesParticipants.Any(sp => sp.PlayerId == p.Id && guildIds.Contains(sp.Series.GuildId)));
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p =>
                EF.Functions.ILike(p.DiscordUsername, $"%{q}%") ||
                (p.RiotId != null && EF.Functions.ILike(p.RiotId, $"%{q}%")));

        return await query
            .OrderBy(p => p.DiscordUsername)
            .Select(p => new PlayerSearchRow(p.Id, p.DiscordUsername, p.RiotId, p.Region == null ? null : p.Region.ToString()))
            .Take(100)
            .ToListAsync(ct);
    }

    public async Task<SeriesDetailDto?> GetSeriesDetailAsync(Guid seriesId, IReadOnlyList<ulong> guildIds, CancellationToken ct = default)
    {
        var series = await db.Series
            .Include(s => s.Teams)
            .Include(s => s.Games).ThenInclude(g => g.Players).ThenInclude(p => p.Player)
            .Include(s => s.Games).ThenInclude(g => g.DraftActions)
            .Include(s => s.Games).ThenInclude(g => g.Stats)
            .FirstOrDefaultAsync(s => s.Id == seriesId && guildIds.Contains(s.GuildId), ct);
        if (series is null)
            return null;

        var teamNames = series.Teams.ToDictionary(t => t.Id, t => t.Name);

        var teamScores = series.Teams
            .OrderBy(t => t.Name)
            .Select(t => new TeamScoreDto(t.Id, t.Name,
                series.Games.Count(g => g.Status == GameStatus.Complete && g.WinnerTeamId == t.Id)))
            .ToList();

        var games = series.Games
            .OrderBy(g => g.GameNumber)
            .Select(g => BuildGameSummary(g, teamNames))
            .ToList();

        return new SeriesDetailDto(
            series.Id, series.Name, series.Status.ToString(), series.BestOf,
            series.Fearless, series.Region.ToString(), teamScores, games);
    }

    public async Task<GameDetailDto?> GetGameDetailAsync(Guid gameId, IReadOnlyList<ulong> guildIds, CancellationToken ct = default)
    {
        var game = await db.Games
            .Include(g => g.BlueTeam)
            .Include(g => g.RedTeam)
            .Include(g => g.Players).ThenInclude(p => p.Player)
            .Include(g => g.DraftActions)
            .Include(g => g.Stats)
            .FirstOrDefaultAsync(g => g.Id == gameId && guildIds.Contains(g.Series.GuildId), ct);
        if (game is null)
            return null;

        var teamNames = new Dictionary<Guid, string>();
        if (game.BlueTeam is not null) teamNames[game.BlueTeam.Id] = game.BlueTeam.Name;
        if (game.RedTeam is not null) teamNames[game.RedTeam.Id] = game.RedTeam.Name;

        var summary = BuildGameSummary(game, teamNames);
        var raw = game.Stats.Select(s => JsonSerializer.Deserialize<JsonElement>(s.Raw)).ToList();
        return new GameDetailDto(summary, raw);
    }

    public async Task<IReadOnlyList<ChampionLeaderboardRow>> ChampionLeaderboardAsync(
        IReadOnlyList<ulong> guildIds, CancellationToken ct = default)
    {
        var rows = await db.GamePlayerStats
            .Where(s => guildIds.Contains(s.Game.Series.GuildId))
            .GroupBy(s => s.ChampionId)
            .Select(g => new { ChampionId = g.Key, Games = g.Count(), Wins = g.Count(x => x.Win) })
            .ToListAsync(ct);

        return rows
            .Select(r => new ChampionLeaderboardRow(r.ChampionId, r.Games, r.Wins, r.Games == 0 ? 0 : (double)r.Wins / r.Games))
            .OrderByDescending(r => r.Games)
            .ToList();
    }

    public async Task<IReadOnlyList<PlayerLeaderboardRow>> PlayerLeaderboardAsync(
        IReadOnlyList<ulong> guildIds, CancellationToken ct = default)
    {
        var rows = await db.GamePlayerStats
            .Where(s => guildIds.Contains(s.Game.Series.GuildId))
            .GroupBy(s => s.PlayerId)
            .Select(g => new
            {
                PlayerId = g.Key,
                Games = g.Count(),
                Wins = g.Count(x => x.Win),
                Kills = g.Sum(x => x.Kills),
                Deaths = g.Sum(x => x.Deaths),
                Assists = g.Sum(x => x.Assists),
            })
            .ToListAsync(ct);

        var names = await db.Players
            .Where(p => rows.Select(r => r.PlayerId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.DiscordUsername, ct);

        return rows
            .Select(r => new PlayerLeaderboardRow(
                r.PlayerId,
                names.GetValueOrDefault(r.PlayerId, "Unknown"),
                r.Games, r.Wins,
                r.Games == 0 ? 0 : (double)r.Wins / r.Games,
                r.Kills, r.Deaths, r.Assists,
                (double)(r.Kills + r.Assists) / Math.Max(1, r.Deaths)))
            .OrderByDescending(r => r.Games)
            .ToList();
    }

    private static GameSummaryDto BuildGameSummary(Game game, IReadOnlyDictionary<Guid, string> teamNames)
    {
        string? Name(Guid? id) => id is { } i && teamNames.TryGetValue(i, out var n) ? n : null;

        IReadOnlyList<int> Bans(TeamSide side) => game.DraftActions
            .Where(d => d.Type == DraftActionType.Ban && d.Side == side)
            .OrderBy(d => d.Sequence)
            .Select(d => d.ChampionId)
            .ToList();

        var players = game.Players
            .OrderBy(p => p.Side)
            .ThenBy(p => p.Player.DiscordUsername)
            .Select(gp =>
            {
                var stat = game.Stats.FirstOrDefault(s => s.PlayerId == gp.PlayerId);
                return new PlayerLineDto(
                    gp.PlayerId, gp.Player.DiscordUsername, gp.Side.ToString().ToLowerInvariant(),
                    gp.Role?.ToString(), stat?.ChampionId ?? gp.PickedChampionId,
                    stat?.Kills, stat?.Deaths, stat?.Assists, stat?.Gold, stat?.Cs, stat?.Damage, stat?.Win);
            })
            .ToList();

        return new GameSummaryDto(
            game.Id, game.GameNumber, game.Status.ToString(),
            Name(game.BlueTeamId), Name(game.RedTeamId), Name(game.WinnerTeamId),
            Bans(TeamSide.Blue), Bans(TeamSide.Red), players);
    }
}
