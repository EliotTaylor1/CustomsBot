namespace CustomsBot.Domain;

public class Series
{
    public Guid Id { get; set; }

    /// <summary>Display name; not unique (multiple concurrent series may share a name).</summary>
    public string Name { get; set; } = null!;

    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }

    public DraftType DraftType { get; set; }
    public bool Fearless { get; set; }
    public GameMap Map { get; set; }
    public Region Region { get; set; }
    public int BestOf { get; set; }

    public SeriesStatus Status { get; set; } = SeriesStatus.Setup;
    public ulong OwnerDiscordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<SeriesTeam> Teams { get; set; } = new List<SeriesTeam>();
    public ICollection<SeriesParticipant> Participants { get; set; } = new List<SeriesParticipant>();
    public ICollection<Game> Games { get; set; } = new List<Game>();
}

/// <summary>
/// A persistent team within a series. Two per series; persistence across games keeps
/// best-of ("a team wins N") well-defined even when blue/red sides swap.
/// </summary>
public class SeriesTeam
{
    public Guid Id { get; set; }

    public Guid SeriesId { get; set; }
    public Series Series { get; set; } = null!;

    /// <summary>Defaults to "Team: {captain}".</summary>
    public string Name { get; set; } = null!;

    public Guid? CaptainPlayerId { get; set; }
    public Player? Captain { get; set; }
}

/// <summary>The player pool for a series (join of Series and Player).</summary>
public class SeriesParticipant
{
    public Guid SeriesId { get; set; }
    public Series Series { get; set; } = null!;

    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;
}
