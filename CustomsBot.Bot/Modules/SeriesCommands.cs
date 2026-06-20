using CustomsBot.Data;
using CustomsBot.Domain;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace CustomsBot.Bot.Modules;

public class SeriesCommands(CustomsBotDbContext db) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("create-series", "Create a new custom-game series")]
    public async Task<InteractionMessageProperties> CreateSeriesAsync(
        [SlashCommandParameter(Description = "Series name")] string name,
        [SlashCommandParameter(Name = "draft-type", Description = "Draft type")] DraftType draftType,
        [SlashCommandParameter(Description = "Picks lock across the whole series")] bool fearless,
        [SlashCommandParameter(Description = "Map")] GameMap map,
        [SlashCommandParameter(Description = "Riot region")] Region region,
        [SlashCommandParameter(Name = "best-of", Description = "Best-of (odd, 1-9)", MinValue = 1, MaxValue = 9)] int bestOf)
    {
        if (bestOf % 2 == 0)
            return new InteractionMessageProperties { Content = "Best-of must be an odd number.", Flags = MessageFlags.Ephemeral };

        var series = new Series
        {
            Id = Guid.NewGuid(),
            Name = name,
            GuildId = Context.Interaction.GuildId ?? 0,
            ChannelId = Context.Channel.Id,
            DraftType = draftType,
            Fearless = fearless,
            Map = map,
            Region = region,
            BestOf = bestOf,
            Status = SeriesStatus.Setup,
            OwnerDiscordId = Context.User.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // Two persistent teams; captains (and their proper names) are assigned later in the lobby.
        series.Teams.Add(new SeriesTeam { Id = Guid.NewGuid(), Name = "Team 1" });
        series.Teams.Add(new SeriesTeam { Id = Guid.NewGuid(), Name = "Team 2" });

        db.Series.Add(series);
        await db.SaveChangesAsync();

        return SeriesPanel.Build(series, []);
    }
}
