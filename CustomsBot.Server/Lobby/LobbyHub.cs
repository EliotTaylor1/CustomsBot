using System.Security.Claims;
using CustomsBot.Server.Auth;
using Microsoft.AspNetCore.SignalR;

namespace CustomsBot.Server.Lobby;

/// <summary>
/// Live lobby sync. Clients send intents only; the server validates, mutates the
/// authoritative state, and broadcasts the new snapshot to everyone in the lobby.
/// Every intent is attributed to the caller's logged-in Discord id (role/ready are
/// self-only; side/rename/start require series owner or captain).
/// </summary>
public class LobbyHub(LobbyService lobbies) : Hub
{
    private static string Group(Guid gameId) => $"lobby-{gameId}";

    private ulong CallerId =>
        ulong.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task JoinLobby(Guid gameId)
    {
        var state = await lobbies.EnsureLobbyAsync(gameId, Context.User!.GuildIds());
        if (state is null)
            return; // Not a lobby or the viewer isn't in this series' server.
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(gameId));
        await Clients.Caller.SendAsync("LobbyUpdated", state);
    }

    public Task AssignSide(Guid gameId, Guid playerId, string side) =>
        Broadcast(gameId, lobbies.AssignSide(gameId, playerId, side, CallerId));

    public Task SetRole(Guid gameId, Guid playerId, string? role) =>
        Broadcast(gameId, lobbies.SetRole(gameId, playerId, role, CallerId));

    public Task SetReady(Guid gameId, Guid playerId, bool ready) =>
        Broadcast(gameId, lobbies.SetReady(gameId, playerId, ready, CallerId));

    public Task SetTeamName(Guid gameId, string side, string name) =>
        Broadcast(gameId, lobbies.SetTeamName(gameId, side, name, CallerId));

    public async Task StartChampSelect(Guid gameId) =>
        await Broadcast(gameId, await lobbies.StartAsync(gameId, CallerId));

    private async Task Broadcast(Guid gameId, LobbyStateDto? state)
    {
        if (state is not null)
            await Clients.Group(Group(gameId)).SendAsync("LobbyUpdated", state);
    }
}
