using System.Collections.Concurrent;
using CustomsBot.Domain;
using CustomsBot.Riot;

namespace CustomsBot.Bot;

/// <summary>
/// Generates one tournament code per game. Registers a provider once per process and a
/// tournament once per series (cached), as the plan prescribes, then creates single-use codes.
/// </summary>
public class TournamentCodeService(IServiceScopeFactory scopeFactory, IConfiguration configuration)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, long> _tournamentsBySeries = new();
    private long? _providerId;

    public async Task<string> CreateGameCodeAsync(
        Series series, int teamSize, IReadOnlyList<string> allowedPuuids, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<TournamentClient>();

        var providerId = await EnsureProviderAsync(client, series.Region, ct);
        var tournamentId = await EnsureTournamentAsync(client, series, providerId, ct);

        var parameters = new TournamentCodeParameters(
            TeamSize: teamSize,
            PickType: "TOURNAMENT_DRAFT",
            MapType: series.Map.ToMapType(),
            SpectatorType: "ALL",
            AllowedParticipants: allowedPuuids);

        var codes = await client.CreateCodesAsync(tournamentId, 1, parameters, ct);
        return codes[0];
    }

    private async Task<long> EnsureProviderAsync(TournamentClient client, Region region, CancellationToken ct)
    {
        if (_providerId is { } cached)
            return cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (_providerId is null)
            {
                var callbackUrl = configuration["Riot:Tournament:CallbackUrl"] ?? "https://example.com/tournament-callback";
                _providerId = await client.RegisterProviderAsync(
                    new ProviderRegistrationParameters(region.ToPlatformCode(), callbackUrl), ct);
            }
            return _providerId.Value;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<long> EnsureTournamentAsync(TournamentClient client, Series series, long providerId, CancellationToken ct)
    {
        if (_tournamentsBySeries.TryGetValue(series.Id, out var cached))
            return cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (!_tournamentsBySeries.TryGetValue(series.Id, out cached))
            {
                cached = await client.RegisterTournamentAsync(
                    new TournamentRegistrationParameters(providerId, series.Name), ct);
                _tournamentsBySeries[series.Id] = cached;
            }
            return cached;
        }
        finally
        {
            _gate.Release();
        }
    }
}
