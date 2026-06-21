using CustomsBot.Domain;

namespace CustomsBot.Server.Lobby;

/// <summary>Public lobby snapshot pushed to clients. Clients render this; they never send state.</summary>
public record LobbyStateDto(
    Guid GameId,
    Guid SeriesId,
    string SeriesName,
    int GameNumber,
    string BlueTeamName,
    string RedTeamName,
    // Discord id as a string: 64-bit snowflakes exceed JS safe-integer range.
    string OwnerDiscordId,
    IReadOnlyList<LobbyPlayerDto> Players,
    bool CanStart,
    bool Started,
    long Sequence);

public record LobbyPlayerDto(
    Guid PlayerId,
    string DiscordId,
    string Username,
    string? Avatar,
    bool HasPuuid,
    bool IsCaptain,
    string Side,
    string? Role,
    bool IsReady);

public record SeriesSummaryDto(
    Guid Id,
    string Name,
    string Status,
    int BestOf,
    int ParticipantCount);

/// <summary>Server-owned mutable lobby state held in memory (see plan §5d).</summary>
internal sealed class LobbyState
{
    public required Guid GameId { get; init; }
    public required Guid SeriesId { get; init; }
    public required ulong GuildId { get; init; }
    public required ulong OwnerDiscordId { get; init; }
    public required string SeriesName { get; init; }
    public required int GameNumber { get; init; }

    public required Guid BlueTeamId { get; init; }
    public required Guid RedTeamId { get; init; }
    public required string BlueTeamName { get; set; }
    public required string RedTeamName { get; set; }

    public required Dictionary<Guid, LobbyPlayer> Players { get; init; }

    public long Sequence { get; set; }
    public bool Started { get; set; }

    public readonly object Gate = new();
}

internal sealed class LobbyPlayer
{
    public required Guid PlayerId { get; init; }
    public required ulong DiscordId { get; init; }
    public required string Username { get; init; }
    public string? Avatar { get; init; }
    public bool HasPuuid { get; init; }
    public bool IsCaptain { get; init; }

    public TeamSide Side { get; set; } = TeamSide.Spectator;
    public Role? Role { get; set; }
    public bool IsReady { get; set; }
}
