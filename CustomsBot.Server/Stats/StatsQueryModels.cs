using System.Text.Json;

namespace CustomsBot.Server.Stats;

public record SeriesSearchRow(Guid Id, string Name, string Status, int BestOf, int ParticipantCount, DateTimeOffset CreatedAt);
public record PlayerSearchRow(Guid Id, string Username, string? RiotId, string? Region);

public record TeamScoreDto(Guid TeamId, string Name, int Wins);

public record SeriesDetailDto(
    Guid Id,
    string Name,
    string Status,
    int BestOf,
    bool Fearless,
    string Region,
    IReadOnlyList<TeamScoreDto> Teams,
    IReadOnlyList<GameSummaryDto> Games);

public record GameSummaryDto(
    Guid Id,
    int GameNumber,
    string Status,
    string? BlueTeamName,
    string? RedTeamName,
    string? WinnerTeamName,
    IReadOnlyList<int> BlueBans,
    IReadOnlyList<int> RedBans,
    IReadOnlyList<PlayerLineDto> Players);

public record PlayerLineDto(
    Guid PlayerId,
    string Username,
    string Side,
    string? Role,
    int? ChampionId,
    int? Kills,
    int? Deaths,
    int? Assists,
    int? Gold,
    int? Cs,
    int? Damage,
    bool? Win);

/// <summary>Per-game detail including the full Match-V5 participant payloads (raw jsonb).</summary>
public record GameDetailDto(GameSummaryDto Summary, IReadOnlyList<JsonElement> RawParticipants);

public record ChampionLeaderboardRow(int ChampionId, int Games, int Wins, double WinRate);
public record PlayerLeaderboardRow(
    Guid PlayerId, string Username, int Games, int Wins, double WinRate,
    int Kills, int Deaths, int Assists, double Kda);
