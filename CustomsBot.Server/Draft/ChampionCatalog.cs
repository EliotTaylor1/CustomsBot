using CustomsBot.Riot;

namespace CustomsBot.Server.Draft;

/// <summary>Caches the Data Dragon champion list once and answers existence checks for the draft.</summary>
public class ChampionCatalog(IServiceScopeFactory scopeFactory)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<Champion>? _all;
    private Dictionary<int, Champion>? _byId;

    public async Task<IReadOnlyList<Champion>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _all!;
    }

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_all is not null)
            return;

        await _gate.WaitAsync(ct);
        try
        {
            if (_all is null)
            {
                using var scope = scopeFactory.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<DataDragonClient>();
                var all = await client.GetChampionsAsync(ct);
                _byId = all.ToDictionary(c => c.Id);
                _all = all;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Existence check. Call <see cref="EnsureLoadedAsync"/> first.</summary>
    public bool Exists(int championId) =>
        _byId is not null && _byId.ContainsKey(championId);
}
