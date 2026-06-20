using System.Text;
using CustomsBot.Data;
using CustomsBot.Domain;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;

namespace CustomsBot.Bot;

public record ManageResult(bool Ok, string Message);

/// <summary>
/// Shared series-management operations behind the <c>/series</c> commands. Both the direct
/// command path and the duplicate-disambiguation select menu call these, so ownership and
/// status rules live in one place.
/// </summary>
public class SeriesManager(CustomsBotDbContext db, IConfiguration configuration)
{
    public static string StatusLabel(SeriesStatus status) => status switch
    {
        SeriesStatus.Setup => "Not started",
        SeriesStatus.InProgress => "In progress",
        SeriesStatus.Complete => "Completed",
        _ => status.ToString()
    };

    private static string ShortId(Guid id) => id.ToString("N")[..8];

    /// <summary>Series owned by the caller that can still be managed (not Completed).</summary>
    public Task<List<Series>> EligibleAsync(ulong ownerId) =>
        db.Series
            .Where(s => s.OwnerDiscordId == ownerId && s.Status != SeriesStatus.Complete)
            .Include(s => s.Teams)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public async Task<string> DescribeAsync(Series series)
    {
        var score = await ScoreAsync(series);
        return $"Bo{series.BestOf} · {score} · {StatusLabel(series.Status)} · {ShortId(series.Id)}";
    }

    private async Task<string> ScoreAsync(Series series)
    {
        var teams = series.Teams.OrderBy(t => t.Name).ToList();
        if (teams.Count < 2)
            return "0–0";

        var counts = await db.Games
            .Where(g => g.SeriesId == series.Id && g.Status == GameStatus.Complete && g.WinnerTeamId != null)
            .GroupBy(g => g.WinnerTeamId)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .ToListAsync();

        int Wins(Guid teamId) => counts.FirstOrDefault(c => c.TeamId == teamId)?.Count ?? 0;
        return $"{Wins(teams[0].Id)}–{Wins(teams[1].Id)}";
    }

    public async Task<EmbedProperties> BuildListAsync(ulong guildId)
    {
        var series = await db.Series
            .Where(s => s.GuildId == guildId && s.Status != SeriesStatus.Complete)
            .Include(s => s.Teams)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var embed = new EmbedProperties().WithTitle("Active series").WithColor(new Color(0x5865F2));
        if (series.Count == 0)
            return embed.WithDescription("No active series. Create one with `/create-series`.");

        foreach (var s in series)
        {
            var score = await ScoreAsync(s);
            embed.AddFields(new EmbedFieldProperties()
                .WithName($"{s.Name}  ·  {ShortId(s.Id)}")
                .WithValue(
                    $"{StatusLabel(s.Status)} · Bo{s.BestOf} · {score}\n" +
                    $"{s.DraftType} · fearless {(s.Fearless ? "on" : "off")} · {s.Region} · owner <@{s.OwnerDiscordId}>"));
        }
        return embed;
    }

    public async Task<ManageResult> StartNewDraftAsync(Guid seriesId, ulong callerId)
    {
        var series = await db.Series.Include(s => s.Games).FirstOrDefaultAsync(s => s.Id == seriesId);
        if (series is null) return new(false, "That series no longer exists.");
        if (series.OwnerDiscordId != callerId) return new(false, "Only the series owner can do that.");
        if (series.Status == SeriesStatus.Complete) return new(false, "That series is completed.");

        var gameNumber = series.Games.Count + 1;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            SeriesId = seriesId,
            GameNumber = gameNumber,
            Status = GameStatus.Lobby,
        };
        db.Games.Add(game);
        if (series.Status == SeriesStatus.Setup)
            series.Status = SeriesStatus.InProgress;
        await db.SaveChangesAsync();

        var baseUrl = configuration["Web:BaseUrl"];
        var where = string.IsNullOrEmpty(baseUrl)
            ? $"open it in the web app (lobby id `{game.Id}`)"
            : $"{baseUrl.TrimEnd('/')}/#/lobby/{game.Id}";
        return new(true, $"Opened the lobby for **{series.Name}** game {gameNumber} — {where}");
    }

    public async Task<ManageResult> EndAsync(Guid seriesId, ulong callerId)
    {
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId);
        if (series is null) return new(false, "That series no longer exists.");
        if (series.OwnerDiscordId != callerId) return new(false, "Only the series owner can do that.");
        if (series.Status == SeriesStatus.Complete) return new(false, "That series is already completed.");

        series.Status = SeriesStatus.Complete;
        await db.SaveChangesAsync();
        return new(true, $"Ended **{series.Name}**.");
    }

    public async Task<ManageResult> EditAsync(Guid seriesId, ulong callerId, string? name, int? bestOf)
    {
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId);
        if (series is null) return new(false, "That series no longer exists.");
        if (series.OwnerDiscordId != callerId) return new(false, "Only the series owner can do that.");
        if (series.Status == SeriesStatus.Complete) return new(false, "Completed series can't be edited.");
        if (name is null && bestOf is null) return new(false, "Provide a new name and/or best-of.");

        if (bestOf is { } bo)
        {
            if (bo % 2 == 0) return new(false, "Best-of must be an odd number.");
            var maxTeamWins = await db.Games
                .Where(g => g.SeriesId == seriesId && g.Status == GameStatus.Complete && g.WinnerTeamId != null)
                .GroupBy(g => g.WinnerTeamId)
                .Select(g => g.Count())
                .OrderByDescending(c => c)
                .FirstOrDefaultAsync();
            var min = Math.Max(1, 2 * maxTeamWins - 1);
            if (bo < min) return new(false, $"Best-of can't drop below {min} (a team already has {maxTeamWins} win(s)).");
            series.BestOf = bo;
        }

        if (name is not null)
            series.Name = name;

        await db.SaveChangesAsync();
        return new(true, $"Updated **{series.Name}** (Bo{series.BestOf}).");
    }

    public async Task<ManageResult> TransferAsync(Guid seriesId, ulong callerId, ulong newOwnerId)
    {
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId);
        if (series is null) return new(false, "That series no longer exists.");
        if (series.OwnerDiscordId != callerId) return new(false, "Only the series owner can do that.");
        if (series.Status == SeriesStatus.Complete) return new(false, "That series is completed.");

        series.OwnerDiscordId = newOwnerId;
        await db.SaveChangesAsync();
        return new(true, $"Transferred **{series.Name}** to <@{newOwnerId}>.");
    }

    public static string EncodeName(string? name) =>
        name is null ? "-" : Convert.ToBase64String(Encoding.UTF8.GetBytes(name));

    public static string? DecodeName(string encoded) =>
        encoded == "-" ? null : Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
}
