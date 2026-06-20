namespace CustomsBot.Domain;

public class Game
{
    public Guid Id { get; set; }

    public Guid SeriesId { get; set; }
    public Series Series { get; set; } = null!;

    public int GameNumber { get; set; }
    public GameStatus Status { get; set; } = GameStatus.Lobby;

    /// <summary>SeriesTeam playing blue this game (side mapping is per-game).</summary>
    public Guid? BlueTeamId { get; set; }
    public SeriesTeam? BlueTeam { get; set; }

    public Guid? RedTeamId { get; set; }
    public SeriesTeam? RedTeam { get; set; }

    public string? TournamentCode { get; set; }
    public string? RiotMatchId { get; set; }

    public Guid? WinnerTeamId { get; set; }
    public SeriesTeam? WinnerTeam { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<GamePlayer> Players { get; set; } = new List<GamePlayer>();
    public ICollection<DraftAction> DraftActions { get; set; } = new List<DraftAction>();
    public ICollection<GamePlayerStats> Stats { get; set; } = new List<GamePlayerStats>();
}

public class GamePlayer
{
    public Guid Id { get; set; }

    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;

    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public TeamSide Side { get; set; }
    public Role? Role { get; set; }
    public bool IsCaptain { get; set; }
    public bool IsReady { get; set; }

    /// <summary>Data Dragon champion id locked in champ select.</summary>
    public int? PickedChampionId { get; set; }
}

public class DraftAction
{
    public Guid Id { get; set; }

    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;

    public int Sequence { get; set; }
    public DraftActionType Type { get; set; }
    public TeamSide Side { get; set; }

    /// <summary>Null for bans.</summary>
    public Guid? PlayerId { get; set; }
    public Player? Player { get; set; }

    public int ChampionId { get; set; }
}

public class GamePlayerStats
{
    public Guid Id { get; set; }

    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;

    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public int ChampionId { get; set; }

    /// <summary>Full Match-V5 participant payload (jsonb).</summary>
    public string Raw { get; set; } = null!;

    // Promoted columns for fast read-model queries / leaderboards.
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int Gold { get; set; }
    public int Cs { get; set; }
    public int Damage { get; set; }
    public bool Win { get; set; }
}
