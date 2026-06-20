using System.Net;
using System.Net.Http.Json;

namespace CustomsBot.Riot;

/// <summary>A linked Riot account as returned by Account-V1.</summary>
public record RiotAccount(string Puuid, string GameName, string TagLine);

/// <summary>Account-V1: resolves a Riot ID (gameName#tagLine) to a PUUID.</summary>
public class RiotAccountClient(HttpClient http)
{
    public async Task<RiotAccount?> GetByRiotIdAsync(
        RegionalRoute route, string gameName, string tagLine, CancellationToken ct = default)
    {
        var url = $"{route.ToHost()}/riot/account/v1/accounts/by-riot-id/" +
                  $"{Uri.EscapeDataString(gameName)}/{Uri.EscapeDataString(tagLine)}";

        using var response = await http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RiotAccount>(ct);
    }
}
