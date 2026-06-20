using System.Text.Json;
using CustomsBot.Data;
using CustomsBot.Domain;
using CustomsBot.Riot;
using Microsoft.EntityFrameworkCore;

namespace CustomsBot.Server.Stats;

public enum RecordResult
{
    Success,
    GameNotFound,
    MatchNotFound,
    AlreadyComplete,
    Invalid
}

/// <summary>
/// Pulls a finished match from Match-V5, writes per-player stats, sets the winning SeriesTeam,
/// completes the game, and auto-completes the series once a team clinches (§5f).
/// </summary>
public class StatsService(CustomsBotDbContext db, MatchV5Client matches)
{
    public async Task<RecordResult> RecordResultAsync(Guid gameId, string matchId, CancellationToken ct = default)
    {
        var game = await db.Games
            .Include(g => g.Series)
            .Include(g => g.Players).ThenInclude(p => p.Player)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct);

        if (game is null)
            return RecordResult.GameNotFound;
        if (game.Status == GameStatus.Complete)
            return RecordResult.AlreadyComplete;
        if (game.BlueTeamId is null || game.RedTeamId is null)
            return RecordResult.Invalid;

        var raw = await matches.GetMatchRawAsync(game.Series.Region.ToRegionalRoute(), matchId, ct);
        if (raw is null)
            return RecordResult.MatchNotFound;

        using var doc = JsonDocument.Parse(raw);
        var participants = doc.RootElement.GetProperty("info").GetProperty("participants");

        TeamSide? winningSide = null;
        foreach (var p in participants.EnumerateArray())
        {
            var puuid = p.GetProperty("puuid").GetString();
            var gp = game.Players.FirstOrDefault(x => x.Player.Puuid == puuid);
            if (gp is null)
                continue; // participant not part of our tracked roster (or unlinked)

            var win = p.GetProperty("win").GetBoolean();
            var cs = p.GetProperty("totalMinionsKilled").GetInt32() + p.GetProperty("neutralMinionsKilled").GetInt32();

            db.GamePlayerStats.Add(new GamePlayerStats
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                PlayerId = gp.PlayerId,
                ChampionId = p.GetProperty("championId").GetInt32(),
                Raw = p.GetRawText(),
                Kills = p.GetProperty("kills").GetInt32(),
                Deaths = p.GetProperty("deaths").GetInt32(),
                Assists = p.GetProperty("assists").GetInt32(),
                Gold = p.GetProperty("goldEarned").GetInt32(),
                Cs = cs,
                Damage = p.GetProperty("totalDamageDealtToChampions").GetInt32(),
                Win = win,
            });

            if (win)
                winningSide = gp.Side;
        }

        game.RiotMatchId = matchId;
        if (winningSide is { } side)
            game.WinnerTeamId = side == TeamSide.Blue ? game.BlueTeamId : game.RedTeamId;
        game.Status = GameStatus.Complete;
        game.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        await EvaluateClinchAsync(game.SeriesId, game.Series.BestOf, ct);
        return RecordResult.Success;
    }

    private async Task EvaluateClinchAsync(Guid seriesId, int bestOf, CancellationToken ct)
    {
        var needed = bestOf / 2 + 1;

        var maxWins = await db.Games
            .Where(g => g.SeriesId == seriesId && g.Status == GameStatus.Complete && g.WinnerTeamId != null)
            .GroupBy(g => g.WinnerTeamId)
            .Select(grp => grp.Count())
            .OrderByDescending(c => c)
            .FirstOrDefaultAsync(ct);

        if (maxWins >= needed)
        {
            var series = await db.Series.FirstAsync(s => s.Id == seriesId, ct);
            if (series.Status != SeriesStatus.Complete)
            {
                series.Status = SeriesStatus.Complete;
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
