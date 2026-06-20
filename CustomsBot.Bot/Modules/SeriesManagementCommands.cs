using CustomsBot.Domain;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace CustomsBot.Bot.Modules;

[SlashCommand("series", "Manage your custom-game series")]
public class SeriesManagementCommands(SeriesManager manager) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SubSlashCommand("list", "List active series in this server")]
    public async Task<InteractionMessageProperties> ListAsync()
    {
        var embed = await manager.BuildListAsync(Context.Interaction.GuildId ?? 0);
        return new InteractionMessageProperties { Embeds = [embed] };
    }

    [SubSlashCommand("new-draft", "Open the next game's lobby")]
    public Task<InteractionMessageProperties> NewDraftAsync() =>
        ResolveAsync("series-nd", "Pick a series to open a draft for:",
            id => manager.StartNewDraftAsync(id, Context.User.Id));

    [SubSlashCommand("end", "End a series early")]
    public Task<InteractionMessageProperties> EndAsync() =>
        ResolveAsync("series-end", "Pick a series to end:",
            id => manager.EndAsync(id, Context.User.Id));

    [SubSlashCommand("edit", "Edit a series' name and/or best-of")]
    public Task<InteractionMessageProperties> EditAsync(
        [SlashCommandParameter(Description = "New name")] string? name = null,
        [SlashCommandParameter(Name = "best-of", Description = "New best-of (odd)", MinValue = 1, MaxValue = 9)] int? bestOf = null)
    {
        var customId = $"series-ed:{bestOf ?? 0}:{SeriesManager.EncodeName(name)}";
        return ResolveAsync(customId, "Pick a series to edit:",
            id => manager.EditAsync(id, Context.User.Id, name, bestOf));
    }

    [SubSlashCommand("transfer-owner", "Transfer series ownership")]
    public Task<InteractionMessageProperties> TransferAsync(
        [SlashCommandParameter(Name = "new-owner", Description = "The new owner")] User newOwner) =>
        ResolveAsync($"series-tr:{newOwner.Id}", "Pick a series to transfer:",
            id => manager.TransferAsync(id, Context.User.Id, newOwner.Id));

    /// <summary>0 eligible → message; 1 → act directly; many → a disambiguation select menu.</summary>
    private async Task<InteractionMessageProperties> ResolveAsync(
        string menuCustomId, string prompt, Func<Guid, Task<ManageResult>> apply)
    {
        var eligible = await manager.EligibleAsync(Context.User.Id);
        if (eligible.Count == 0)
            return Ephemeral("You have no eligible series.");
        if (eligible.Count == 1)
            return Ephemeral((await apply(eligible[0].Id)).Message);

        var options = new List<StringMenuSelectOptionProperties>();
        foreach (var s in eligible)
            options.Add(new StringMenuSelectOptionProperties(s.Name, s.Id.ToString())
                .WithDescription(await manager.DescribeAsync(s)));

        var menu = new StringMenuProperties(menuCustomId, options) { Placeholder = "Select a series" };
        return new InteractionMessageProperties { Content = prompt, Components = [menu], Flags = MessageFlags.Ephemeral };
    }

    private static InteractionMessageProperties Ephemeral(string content) =>
        new() { Content = content, Flags = MessageFlags.Ephemeral };
}
