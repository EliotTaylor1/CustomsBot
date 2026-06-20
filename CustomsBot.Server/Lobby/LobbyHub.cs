using Microsoft.AspNetCore.SignalR;

namespace CustomsBot.Server.Lobby;

/// <summary>
/// Live lobby sync. Clients send intents only; the server validates, mutates the
/// authoritative state, and broadcasts the new snapshot to everyone in the lobby.
/// </summary>
public class LobbyHub(LobbyService lobbies) : Hub
{
    private static string Group(Guid gameId) => $"lobby-{gameId}";

    public async Task JoinLobby(Guid gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(gameId));
        var state = await lobbies.EnsureLobbyAsync(gameId);
        if (state is not null)
            await Clients.Caller.SendAsync("LobbyUpdated", state);
    }

    public Task AssignSide(Guid gameId, Guid playerId, string side) =>
        Broadcast(gameId, lobbies.AssignSide(gameId, playerId, side));

    public Task SetRole(Guid gameId, Guid playerId, string? role) =>
        Broadcast(gameId, lobbies.SetRole(gameId, playerId, role));

    public Task SetReady(Guid gameId, Guid playerId, bool ready) =>
        Broadcast(gameId, lobbies.SetReady(gameId, playerId, ready));

    public Task SetTeamName(Guid gameId, string side, string name) =>
        Broadcast(gameId, lobbies.SetTeamName(gameId, side, name));

    public async Task StartChampSelect(Guid gameId) =>
        await Broadcast(gameId, await lobbies.StartAsync(gameId));

    private async Task Broadcast(Guid gameId, LobbyStateDto? state)
    {
        if (state is not null)
            await Clients.Group(Group(gameId)).SendAsync("LobbyUpdated", state);
    }
}
