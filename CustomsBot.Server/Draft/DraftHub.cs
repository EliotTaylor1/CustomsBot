using Microsoft.AspNetCore.SignalR;

namespace CustomsBot.Server.Draft;

/// <summary>
/// Live champion select. Clients send intents (ban/pick/swap/claim) carrying their slot
/// token + expected sequence; the server validates and broadcasts the new snapshot. Rejected
/// intents re-sync only the caller so a stale client catches up.
/// </summary>
public class DraftHub(DraftService drafts) : Hub
{
    private static string Group(Guid gameId) => $"draft-{gameId}";

    public async Task JoinDraft(Guid gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(gameId));
        var state = await drafts.EnsureDraftAsync(gameId);
        if (state is not null)
            await Clients.Caller.SendAsync("DraftUpdated", state);
    }

    public async Task<ClaimResultDto?> ClaimSlot(Guid gameId, Guid slotId, string? token)
    {
        var result = await drafts.ClaimSlotAsync(gameId, slotId, token);
        if (result is not null)
            await Clients.Group(Group(gameId)).SendAsync("DraftUpdated", result.State);
        return result;
    }

    public Task Ban(Guid gameId, string token, int championId, long expectedSequence) =>
        BroadcastOrResync(gameId, drafts.BanAsync(gameId, token, championId, expectedSequence));

    public Task Pick(Guid gameId, string token, int championId, long expectedSequence) =>
        BroadcastOrResync(gameId, drafts.PickAsync(gameId, token, championId, expectedSequence));

    public Task Swap(Guid gameId, string token, Guid slotIdA, Guid slotIdB, long expectedSequence) =>
        BroadcastOrResync(gameId, drafts.SwapAsync(gameId, token, slotIdA, slotIdB, expectedSequence));

    private async Task BroadcastOrResync(Guid gameId, Task<DraftStateDto?> action)
    {
        var state = await action;
        if (state is not null)
        {
            await Clients.Group(Group(gameId)).SendAsync("DraftUpdated", state);
        }
        else
        {
            var current = drafts.GetState(gameId);
            if (current is not null)
                await Clients.Caller.SendAsync("DraftUpdated", current);
        }
    }
}
