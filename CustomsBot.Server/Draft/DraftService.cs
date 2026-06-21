using System.Collections.Concurrent;
using CustomsBot.Data;
using CustomsBot.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomsBot.Server.Draft;

/// <summary>
/// Server-authoritative champion-select state machine (§5c/§5d). One in-memory state per
/// game; every intent is validated (phase, turn, slot ownership, champion legality,
/// expected-sequence) before it mutates state. Clients only send intents.
/// </summary>
public class DraftService(IServiceScopeFactory scopeFactory, ChampionCatalog catalog)
{
    private static readonly Role[] RoleOrder = [Role.Top, Role.Jungle, Role.Mid, Role.Bot, Role.Support];

    private readonly ConcurrentDictionary<Guid, DraftState> _drafts = new();

    /// <summary>Lazily builds draft state from the persisted (drafting) game. Returns null if not draftable.</summary>
    public async Task<DraftStateDto?> EnsureDraftAsync(Guid gameId, IReadOnlyList<ulong> guildIds, CancellationToken ct = default)
    {
        if (_drafts.TryGetValue(gameId, out var existing))
            return guildIds.Contains(existing.GuildId) ? ToDto(existing) : null;

        await catalog.EnsureLoadedAsync(ct);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomsBotDbContext>();

        var game = await db.Games
            .Include(g => g.Series)
            .Include(g => g.BlueTeam)
            .Include(g => g.RedTeam)
            .Include(g => g.Players).ThenInclude(p => p.Player)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct);

        if (game is null || game.Status != GameStatus.Drafting ||
            game.BlueTeamId is null || game.RedTeamId is null ||
            !guildIds.Contains(game.Series.GuildId))
            return null;

        List<DraftSlot> SlotsFor(TeamSide side) => game.Players
            .Where(p => p.Side == side)
            .Select(p => new DraftSlot
            {
                SlotId = p.Id,
                PlayerId = p.PlayerId,
                Username = p.Player.DiscordUsername,
                Side = side,
                Role = p.Role,
            })
            .OrderBy(s => s.Role is { } r ? Array.IndexOf(RoleOrder, r) : int.MaxValue)
            .ThenBy(s => s.Username)
            .ToList();

        HashSet<int> fearless = [];
        if (game.Series.Fearless)
        {
            var priorPicks = await db.DraftActions
                .Where(d => d.Type == DraftActionType.Pick
                            && d.Game.SeriesId == game.SeriesId
                            && d.Game.GameNumber < game.GameNumber)
                .Select(d => d.ChampionId)
                .ToListAsync(ct);
            fearless = [.. priorPicks];
        }

        var blueScore = await db.Games.CountAsync(g => g.SeriesId == game.SeriesId && g.WinnerTeamId == game.BlueTeamId, ct);
        var redScore = await db.Games.CountAsync(g => g.SeriesId == game.SeriesId && g.WinnerTeamId == game.RedTeamId, ct);

        var state = new DraftState
        {
            GameId = gameId,
            SeriesId = game.SeriesId,
            GuildId = game.Series.GuildId,
            GameNumber = game.GameNumber,
            BlueTeamId = game.BlueTeamId.Value,
            RedTeamId = game.RedTeamId.Value,
            BlueTeamName = game.BlueTeam!.Name,
            RedTeamName = game.RedTeam!.Name,
            BlueScore = blueScore,
            RedScore = redScore,
            BlueSlots = SlotsFor(TeamSide.Blue),
            RedSlots = SlotsFor(TeamSide.Red),
            FearlessExcluded = fearless,
        };

        // Another caller may have created it concurrently; keep the winner.
        var actual = _drafts.GetOrAdd(gameId, state);
        return ToDto(actual);
    }

    public DraftStateDto? GetState(Guid gameId) =>
        _drafts.TryGetValue(gameId, out var s) ? ToDto(s) : null;

    public async Task<ClaimResultDto?> ClaimSlotAsync(Guid gameId, Guid slotId, string? token)
    {
        if (!_drafts.TryGetValue(gameId, out var state))
            return null;

        await state.Gate.WaitAsync();
        try
        {
            var slot = FindSlot(state, slotId);
            if (slot is null)
                return null;

            if (state.SlotTokens.TryGetValue(slotId, out var existing))
                // Re-claim only if the caller presents the matching token (reconnect); else it's taken.
                return existing == token ? new ClaimResultDto(existing, slotId, ToDto(state)) : null;

            var newToken = Guid.NewGuid().ToString("N");
            state.SlotTokens[slotId] = newToken;
            return new ClaimResultDto(newToken, slotId, ToDto(state));
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public Task<DraftStateDto?> BanAsync(Guid gameId, string token, int championId, long expectedSequence) =>
        ActAsync(gameId, token, expectedSequence, DraftActionType.Ban, championId);

    public Task<DraftStateDto?> PickAsync(Guid gameId, string token, int championId, long expectedSequence) =>
        ActAsync(gameId, token, expectedSequence, DraftActionType.Pick, championId);

    public async Task<DraftStateDto?> SwapAsync(Guid gameId, string token, Guid slotIdA, Guid slotIdB, long expectedSequence)
    {
        if (!_drafts.TryGetValue(gameId, out var state))
            return null;

        await state.Gate.WaitAsync();
        try
        {
            if (state.Complete || state.Sequence != expectedSequence)
                return null;

            var owner = SlotByToken(state, token);
            var a = FindSlot(state, slotIdA);
            var b = FindSlot(state, slotIdB);
            if (owner is null || a is null || b is null)
                return null;
            if (a.Side != owner.Side || b.Side != owner.Side)
                return null;
            if (a.ChampionId is null || b.ChampionId is null)
                return null;

            (a.ChampionId, b.ChampionId) = (b.ChampionId, a.ChampionId);
            state.Sequence++;
            return ToDto(state);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private async Task<DraftStateDto?> ActAsync(
        Guid gameId, string token, long expectedSequence, DraftActionType type, int championId)
    {
        if (!_drafts.TryGetValue(gameId, out var state))
            return null;

        await state.Gate.WaitAsync();
        try
        {
            if (state.Complete || state.Sequence != expectedSequence)
                return null;

            var step = DraftSteps.All[state.CurrentStep];
            if (step.Type != type)
                return null;

            var owner = SlotByToken(state, token);
            if (owner is null || owner.Side != step.Side)
                return null;

            if (!catalog.Exists(championId) || IsUsedThisGame(state, championId))
                return null;

            if (type == DraftActionType.Ban)
            {
                (step.Side == TeamSide.Blue ? state.BlueBans : state.RedBans).Add(championId);
                state.Actions.Add(new DraftActionRecord(state.CurrentStep, DraftActionType.Ban, step.Side, null, championId));
            }
            else
            {
                if (state.FearlessExcluded.Contains(championId))
                    return null;

                var slot = NextOpenSlot(state, step.Side);
                if (slot is null)
                    return null;
                slot.ChampionId = championId;
                state.Actions.Add(new DraftActionRecord(state.CurrentStep, DraftActionType.Pick, step.Side, slot.PlayerId, championId));
            }

            state.CurrentStep++;
            state.Sequence++;

            if (state.Complete && !state.Persisted)
            {
                state.Persisted = true;
                await PersistCompletionAsync(state);
            }

            return ToDto(state);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private async Task PersistCompletionAsync(DraftState state)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomsBotDbContext>();

        var game = await db.Games.Include(g => g.Players).FirstOrDefaultAsync(g => g.Id == state.GameId);
        if (game is null)
            return;

        foreach (var a in state.Actions)
        {
            db.DraftActions.Add(new DraftAction
            {
                Id = Guid.NewGuid(),
                GameId = state.GameId,
                Sequence = a.Sequence,
                Type = a.Type,
                Side = a.Side,
                PlayerId = a.PlayerId,
                ChampionId = a.ChampionId,
            });
        }

        foreach (var slot in state.BlueSlots.Concat(state.RedSlots))
        {
            var gp = game.Players.FirstOrDefault(p => p.Id == slot.SlotId);
            if (gp is not null)
                gp.PickedChampionId = slot.ChampionId;
        }

        game.Status = GameStatus.AwaitingResult;
        await db.SaveChangesAsync();

        // Notify the bot to generate the tournament code and post it (best-effort).
        var notifier = scope.ServiceProvider.GetRequiredService<BotNotifier>();
        await notifier.NotifyGameReadyAsync(state.GameId);
    }

    private static bool IsUsedThisGame(DraftState state, int championId) =>
        state.BlueBans.Contains(championId) ||
        state.RedBans.Contains(championId) ||
        state.BlueSlots.Concat(state.RedSlots).Any(s => s.ChampionId == championId);

    private static DraftSlot? NextOpenSlot(DraftState state, TeamSide side) =>
        (side == TeamSide.Blue ? state.BlueSlots : state.RedSlots).FirstOrDefault(s => s.ChampionId is null);

    private static DraftSlot? FindSlot(DraftState state, Guid slotId) =>
        state.BlueSlots.Concat(state.RedSlots).FirstOrDefault(s => s.SlotId == slotId);

    private static DraftSlot? SlotByToken(DraftState state, string token)
    {
        var slotId = state.SlotTokens.FirstOrDefault(kv => kv.Value == token).Key;
        return slotId == Guid.Empty ? null : FindSlot(state, slotId);
    }

    private static DraftStateDto ToDto(DraftState state)
    {
        var complete = state.Complete;
        var step = complete ? default : DraftSteps.All[state.CurrentStep];
        var currentSide = complete ? (TeamSide?)null : step.Side;
        var currentPickSlot = !complete && step.Type == DraftActionType.Pick
            ? NextOpenSlot(state, step.Side)
            : null;

        DraftSlotDto Map(DraftSlot s) => new(
            s.SlotId, s.PlayerId, s.Username, s.Role?.ToString(), s.ChampionId,
            currentPickSlot is not null && currentPickSlot.SlotId == s.SlotId,
            state.SlotTokens.ContainsKey(s.SlotId));

        return new DraftStateDto(
            state.GameId,
            state.SeriesId,
            state.BlueTeamName,
            state.RedTeamName,
            state.BlueScore,
            state.RedScore,
            complete ? "complete" : step.Type.ToString().ToLowerInvariant(),
            currentSide?.ToString().ToLowerInvariant(),
            state.CurrentStep,
            DraftSteps.All.Length,
            state.BlueSlots.Select(Map).ToList(),
            state.RedSlots.Select(Map).ToList(),
            [.. state.BlueBans],
            [.. state.RedBans],
            [.. state.FearlessExcluded],
            complete,
            state.Sequence);
    }
}
