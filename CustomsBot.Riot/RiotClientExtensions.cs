using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CustomsBot.Riot;

public static class RiotClientExtensions
{
    /// <summary>
    /// Registers Riot API clients. The API key is read from configuration key <c>Riot:ApiKey</c>
    /// and sent as the <c>X-Riot-Token</c> header (stub or production key).
    /// </summary>
    public static IHostApplicationBuilder AddRiotClients(this IHostApplicationBuilder builder)
    {
        var apiKey = builder.Configuration["Riot:ApiKey"] ?? string.Empty;

        builder.Services.AddHttpClient<RiotAccountClient>(client =>
        {
            client.DefaultRequestHeaders.Add("X-Riot-Token", apiKey);
        });

        builder.Services.AddHttpClient<MatchV5Client>(client =>
        {
            client.DefaultRequestHeaders.Add("X-Riot-Token", apiKey);
        });

        // Data Dragon is public static data — no API key.
        builder.Services.AddHttpClient<DataDragonClient>();

        // Tournament-V5: stub by default; flip Riot:Tournament:UseStub to use the production path.
        var useStub = !bool.TryParse(builder.Configuration["Riot:Tournament:UseStub"], out var stub) || stub;
        var tournamentRegion = builder.Configuration["Riot:Tournament:Region"] ?? "americas";
        var tournamentSegment = useStub ? "tournament-stub" : "tournament";
        builder.Services.AddHttpClient<TournamentClient>(client =>
        {
            client.BaseAddress = new Uri($"https://{tournamentRegion}.api.riotgames.com/lol/{tournamentSegment}/v5/");
            client.DefaultRequestHeaders.Add("X-Riot-Token", apiKey);
        });

        return builder;
    }
}
