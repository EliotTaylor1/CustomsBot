using System.Net.Http.Json;

namespace CustomsBot.Riot;

/// <summary>A champion from Data Dragon static data. <see cref="Id"/> is the numeric Riot key.</summary>
public record Champion(int Id, string Name, string ImageUrl);

/// <summary>
/// Data Dragon static-data client (no API key). Resolves the latest version and the
/// champion list. Callers should cache the result by version.
/// </summary>
public class DataDragonClient(HttpClient http)
{
    private const string Base = "https://ddragon.leagueoflegends.com";

    public async Task<IReadOnlyList<Champion>> GetChampionsAsync(CancellationToken ct = default)
    {
        var versions = await http.GetFromJsonAsync<string[]>($"{Base}/api/versions.json", ct);
        var version = versions is { Length: > 0 }
            ? versions[0]
            : throw new InvalidOperationException("Data Dragon returned no versions.");

        var response = await http.GetFromJsonAsync<ChampionListResponse>(
            $"{Base}/cdn/{version}/data/en_US/champion.json", ct)
            ?? throw new InvalidOperationException("Data Dragon returned no champion data.");

        return response.Data.Values
            .Select(c => new Champion(
                int.Parse(c.Key),
                c.Name,
                $"{Base}/cdn/{version}/img/champion/{c.Image.Full}"))
            .OrderBy(c => c.Name)
            .ToList();
    }

    private record ChampionListResponse(Dictionary<string, ChampionData> Data);
    private record ChampionData(string Key, string Name, ChampionImage Image);
    private record ChampionImage(string Full);
}
