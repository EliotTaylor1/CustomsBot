using CustomsBot.Data;
using CustomsBot.Domain;
using CustomsBot.Riot;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace CustomsBot.Bot;

public record GameReadyRequest(Guid GameId);

/// <summary>
/// Internal endpoint the API calls when a draft completes: generates the tournament code and
/// posts it — with each player's locked champion — to the series channel.
/// </summary>
public static class GameReadyEndpoint
{
    public static async Task<IResult> HandleAsync(
        GameReadyRequest request,
        CustomsBotDbContext db,
        TournamentCodeService tournaments,
        DataDragonClient dataDragon,
        GatewayClient discord,
        CancellationToken ct)
    {
        var game = await db.Games
            .Include(g => g.Series)
            .Include(g => g.BlueTeam)
            .Include(g => g.RedTeam)
            .Include(g => g.Players).ThenInclude(p => p.Player)
            .FirstOrDefaultAsync(g => g.Id == request.GameId, ct);

        if (game is null || game.Status != GameStatus.AwaitingResult)
            return Results.NotFound();

        // Idempotent: if a code already exists, don't generate/post again.
        if (game.TournamentCode is not null)
            return Results.Ok();

        var roster = game.Players.Where(p => p.Side != TeamSide.Spectator).ToList();
        var puuids = roster.Select(p => p.Player.Puuid).OfType<string>().ToList();
        var teamSize = Math.Max(1, roster.Count(p => p.Side == TeamSide.Blue));

        var code = await tournaments.CreateGameCodeAsync(game.Series, teamSize, puuids, ct);
        game.TournamentCode = code;
        await db.SaveChangesAsync(ct);

        var champions = (await dataDragon.GetChampionsAsync(ct)).ToDictionary(c => c.Id, c => c.Name);

        string Line(GamePlayer p)
        {
            var champ = p.PickedChampionId is { } id && champions.TryGetValue(id, out var name) ? name : "—";
            var role = p.Role?.ToString() ?? "";
            return $"**{p.Player.DiscordUsername}** ({role}) — {champ}";
        }

        var embed = new EmbedProperties()
            .WithTitle($"{game.Series.Name} — Game {game.GameNumber}")
            .WithColor(new Color(0x57F287))
            .WithDescription($"Tournament code: `{code}`\nEnter it in the League client to create the lobby.")
            .AddFields(
                new EmbedFieldProperties().WithName($"🔵 {game.BlueTeam!.Name}")
                    .WithValue(string.Join("\n", roster.Where(p => p.Side == TeamSide.Blue).Select(Line)) is { Length: > 0 } b ? b : "—")
                    .WithInline(true),
                new EmbedFieldProperties().WithName($"🔴 {game.RedTeam!.Name}")
                    .WithValue(string.Join("\n", roster.Where(p => p.Side == TeamSide.Red).Select(Line)) is { Length: > 0 } r ? r : "—")
                    .WithInline(true));

        await discord.Rest.SendMessageAsync(
            game.Series.ChannelId,
            new MessageProperties { Embeds = [embed] },
            cancellationToken: ct);

        return Results.Ok();
    }
}
