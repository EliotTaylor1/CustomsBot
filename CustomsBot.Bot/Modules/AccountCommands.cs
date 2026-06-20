using CustomsBot.Data;
using CustomsBot.Domain;
using CustomsBot.Riot;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace CustomsBot.Bot.Modules;

public class AccountCommands(CustomsBotDbContext db, RiotAccountClient accounts)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("link-account", "Link your Riot account so you can be issued tournament codes")]
    public async Task<InteractionMessageProperties> LinkAccountAsync(
        [SlashCommandParameter(Name = "riot-id", Description = "Your Riot ID, e.g. Faker#KR1")] string riotId,
        [SlashCommandParameter(Description = "Your account's region")] Region region)
    {
        var parts = riotId.Split('#', 2);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            return Ephemeral("Riot ID must be in the form `gameName#tagLine`.");

        var account = await accounts.GetByRiotIdAsync(region.ToRegionalRoute(), parts[0], parts[1]);
        if (account is null)
            return Ephemeral($"Couldn't find Riot account `{riotId}` in {region}.");

        var player = await db.Players.FirstOrDefaultAsync(p => p.DiscordId == Context.User.Id);
        if (player is null)
        {
            player = new Player { Id = Guid.NewGuid(), DiscordId = Context.User.Id };
            db.Players.Add(player);
        }

        player.DiscordUsername = Context.User.Username;
        player.DiscordAvatar = Context.User.GetAvatarUrl()?.ToString();
        player.RiotId = $"{account.GameName}#{account.TagLine}";
        player.Puuid = account.Puuid;
        player.Region = region;
        await db.SaveChangesAsync();

        return Ephemeral($"Linked **{player.RiotId}** ✅");
    }

    private static InteractionMessageProperties Ephemeral(string content) =>
        new() { Content = content, Flags = MessageFlags.Ephemeral };
}
