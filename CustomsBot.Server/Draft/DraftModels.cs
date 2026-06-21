using CustomsBot.Domain;

namespace CustomsBot.Server.Draft;

/// <summary>Public draft snapshot pushed to clients. Clients render this; they send only intents.</summary>
public record DraftStateDto(
    Guid GameId,
    Guid SeriesId,
    string BlueTeamName,
    string RedTeamName,
    int BlueScore,
    int RedScore,
    string Phase,          // "ban" | "pick" | "complete"
    string? CurrentSide,   // "blue" | "red" | null
    int StepIndex,
    int TotalSteps,
    IReadOnlyList<DraftSlotDto> BlueSlots,
    IReadOnlyList<DraftSlotDto> RedSlots,
    IReadOnlyList<int> BlueBans,
    IReadOnlyList<int> RedBans,
    IReadOnlyList<int> FearlessExcluded,
    bool Complete,
    long Sequence);

public record DraftSlotDto(
    Guid SlotId,
    Guid PlayerId,
    string Username,
    string? Role,
    int? ChampionId,
    bool IsCurrentPick,
    bool Claimed);

public record ClaimResultDto(string Token, Guid SlotId, DraftStateDto State);

internal sealed class DraftSlot
{
    public required Guid SlotId { get; init; }
    public required Guid PlayerId { get; init; }
    public required string Username { get; init; }
    public required TeamSide Side { get; init; }
    public Role? Role { get; init; }
    public int? ChampionId { get; set; }
}

internal sealed record DraftActionRecord(int Sequence, DraftActionType Type, TeamSide Side, Guid? PlayerId, int ChampionId);

internal sealed class DraftState
{
    public required Guid GameId { get; init; }
    public required Guid SeriesId { get; init; }
    public required ulong GuildId { get; init; }
    public required int GameNumber { get; init; }
    public required Guid BlueTeamId { get; init; }
    public required Guid RedTeamId { get; init; }
    public required string BlueTeamName { get; init; }
    public required string RedTeamName { get; init; }
    public required int BlueScore { get; init; }
    public required int RedScore { get; init; }

    public required List<DraftSlot> BlueSlots { get; init; }
    public required List<DraftSlot> RedSlots { get; init; }

    public List<int> BlueBans { get; } = [];
    public List<int> RedBans { get; } = [];
    public required HashSet<int> FearlessExcluded { get; init; }
    public List<DraftActionRecord> Actions { get; } = [];

    public int CurrentStep { get; set; }
    public long Sequence { get; set; }
    public bool Persisted { get; set; }

    /// <summary>slotId → one-time claim token. Slots without a token are unclaimed; spectators have none.</summary>
    public Dictionary<Guid, string> SlotTokens { get; } = [];

    public readonly SemaphoreSlim Gate = new(1, 1);

    public bool Complete => CurrentStep >= DraftSteps.All.Length;
}

/// <summary>Standard tournament draft order (§5c).</summary>
internal static class DraftSteps
{
    public static readonly (TeamSide Side, DraftActionType Type)[] All =
    [
        // Ban phase 1: B R B R B R
        (TeamSide.Blue, DraftActionType.Ban), (TeamSide.Red, DraftActionType.Ban),
        (TeamSide.Blue, DraftActionType.Ban), (TeamSide.Red, DraftActionType.Ban),
        (TeamSide.Blue, DraftActionType.Ban), (TeamSide.Red, DraftActionType.Ban),
        // Pick phase 1: B | R R | B B | R
        (TeamSide.Blue, DraftActionType.Pick),
        (TeamSide.Red, DraftActionType.Pick), (TeamSide.Red, DraftActionType.Pick),
        (TeamSide.Blue, DraftActionType.Pick), (TeamSide.Blue, DraftActionType.Pick),
        (TeamSide.Red, DraftActionType.Pick),
        // Ban phase 2: R B R B
        (TeamSide.Red, DraftActionType.Ban), (TeamSide.Blue, DraftActionType.Ban),
        (TeamSide.Red, DraftActionType.Ban), (TeamSide.Blue, DraftActionType.Ban),
        // Pick phase 2: R | B B | R
        (TeamSide.Red, DraftActionType.Pick),
        (TeamSide.Blue, DraftActionType.Pick), (TeamSide.Blue, DraftActionType.Pick),
        (TeamSide.Red, DraftActionType.Pick),
    ];
}
