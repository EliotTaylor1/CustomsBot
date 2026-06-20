using System.Net.Http.Json;

namespace CustomsBot.Riot;

// Request bodies mirror Tournament-V5 / tournament-stub-v5. camelCase is applied on send.
public record ProviderRegistrationParameters(string Region, string Url);
public record TournamentRegistrationParameters(long ProviderId, string Name);

public record TournamentCodeParameters(
    int TeamSize,
    string PickType,
    string MapType,
    string SpectatorType,
    IReadOnlyList<string> AllowedParticipants);

/// <summary>
/// Tournament-V5 client. The base address (stub vs production path + regional host) and the
/// API key are configured in <see cref="RiotClientExtensions.AddRiotClients"/>, so swapping to
/// the real key/endpoint at phases 6–7 needs no code change here.
/// </summary>
public class TournamentClient(HttpClient http)
{
    public async Task<long> RegisterProviderAsync(ProviderRegistrationParameters parameters, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("providers", parameters, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<long>(ct);
    }

    public async Task<long> RegisterTournamentAsync(TournamentRegistrationParameters parameters, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("tournaments", parameters, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<long>(ct);
    }

    public async Task<IReadOnlyList<string>> CreateCodesAsync(
        long tournamentId, int count, TournamentCodeParameters parameters, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"codes?count={count}&tournamentId={tournamentId}", parameters, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<string>>(ct) ?? [];
    }
}
