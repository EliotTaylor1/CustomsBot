using System.Net;

namespace CustomsBot.Riot;

/// <summary>
/// Match-V5: fetches a finished match by id (<c>{PLATFORM}_{gameId}</c>, e.g. <c>NA1_456</c>).
/// Returns the raw JSON so the full participant payload can be stored verbatim (jsonb).
/// </summary>
public class MatchV5Client(HttpClient http)
{
    public async Task<string?> GetMatchRawAsync(RegionalRoute route, string matchId, CancellationToken ct = default)
    {
        var url = $"{route.ToHost()}/lol/match/v5/matches/{Uri.EscapeDataString(matchId)}";

        using var response = await http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
