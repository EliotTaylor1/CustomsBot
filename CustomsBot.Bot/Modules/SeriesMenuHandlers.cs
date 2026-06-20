using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace CustomsBot.Bot.Modules;

/// <summary>
/// Completes a <c>/series</c> action after the owner picks from the disambiguation menu.
/// The chosen series id arrives in <c>SelectedValues</c>; operation params ride in the custom id.
/// </summary>
public class SeriesMenuHandlers(SeriesManager manager) : ComponentInteractionModule<StringMenuInteractionContext>
{
    [ComponentInteraction("series-nd")]
    public async Task<InteractionMessageProperties> NewDraftAsync() =>
        Ephemeral((await manager.StartNewDraftAsync(Selected(), Context.User.Id)).Message);

    [ComponentInteraction("series-end")]
    public async Task<InteractionMessageProperties> EndAsync() =>
        Ephemeral((await manager.EndAsync(Selected(), Context.User.Id)).Message);

    [ComponentInteraction("series-tr")]
    public async Task<InteractionMessageProperties> TransferAsync(ulong newOwnerId) =>
        Ephemeral((await manager.TransferAsync(Selected(), Context.User.Id, newOwnerId)).Message);

    [ComponentInteraction("series-ed")]
    public async Task<InteractionMessageProperties> EditAsync(int bestOf, string nameEncoded) =>
        Ephemeral((await manager.EditAsync(
            Selected(), Context.User.Id, SeriesManager.DecodeName(nameEncoded), bestOf == 0 ? null : bestOf)).Message);

    private Guid Selected() => Guid.Parse(Context.SelectedValues[0]);

    private static InteractionMessageProperties Ephemeral(string content) =>
        new() { Content = content, Flags = MessageFlags.Ephemeral };
}
