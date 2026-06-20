using CustomsBot.Data;
using CustomsBot.Domain;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace CustomsBot.Bot.Modules;

public class SeriesPlayerPanel(CustomsBotDbContext db) : ComponentInteractionModule<UserMenuInteractionContext>
{
    [ComponentInteraction(SeriesPanel.AddPlayersId)]
    public async Task AddPlayersAsync(Guid seriesId)
    {
        var series = await db.Series
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == seriesId);

        if (series is null)
        {
            await RespondEphemeralAsync("That series no longer exists.");
            return;
        }

        if (series.OwnerDiscordId != Context.User.Id)
        {
            await RespondEphemeralAsync("Only the series owner can add players.");
            return;
        }

        foreach (var user in Context.SelectedValues)
        {
            var player = await db.Players.FirstOrDefaultAsync(p => p.DiscordId == user.Id);
            if (player is null)
            {
                player = new Player { Id = Guid.NewGuid(), DiscordId = user.Id };
                db.Players.Add(player);
            }

            player.DiscordUsername = user.Username;
            player.DiscordAvatar = user.GetAvatarUrl()?.ToString();

            if (series.Participants.All(p => p.PlayerId != player.Id))
                series.Participants.Add(new SeriesParticipant { SeriesId = series.Id, Player = player });
        }

        await db.SaveChangesAsync();

        var names = await db.SeriesParticipants
            .Where(p => p.SeriesId == series.Id)
            .Select(p => p.Player.DiscordUsername)
            .ToListAsync();

        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(message =>
        {
            message.Embeds = [SeriesPanel.BuildEmbed(series, names)];
            message.Components = [SeriesPanel.BuildMenu(series.Id)];
        }));
    }

    private Task RespondEphemeralAsync(string content) =>
        Context.Interaction.SendResponseAsync(InteractionCallback.Message(
            new InteractionMessageProperties { Content = content, Flags = MessageFlags.Ephemeral }));
}
