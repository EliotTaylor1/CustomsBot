namespace CustomsBot.Domain;

/// <summary>setup = Not started, in_progress = In progress, complete = Completed.</summary>
public enum SeriesStatus
{
    Setup,
    InProgress,
    Complete
}

public enum GameStatus
{
    Lobby,
    Drafting,
    AwaitingResult,
    Complete
}

public enum TeamSide
{
    Blue,
    Red,
    Spectator
}

public enum DraftActionType
{
    Ban,
    Pick
}

/// <summary>
/// Draft order used in champion select. Only the standard tournament draft (§5c) is
/// implemented today; the column is typed so additional orders can be added later.
/// </summary>
public enum DraftType
{
    Tournament
}

public enum GameMap
{
    SummonersRift,
    HowlingAbyss
}

public enum Role
{
    Top,
    Jungle,
    Mid,
    Bot,
    Support
}

/// <summary>Riot platform routing values; a series is scoped to one region.</summary>
public enum Region
{
    Na1,
    Br1,
    La1,
    La2,
    Euw1,
    Eun1,
    Tr1,
    Ru,
    Kr,
    Jp1,
    Oc1
}
