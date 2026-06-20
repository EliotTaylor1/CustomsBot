using System.Net.Http.Json;

namespace CustomsBot.Server.Draft;

/// <summary>
/// Notifies the bot that a draft finished. Best-effort: a failed notify is logged, not thrown,
/// so it never breaks draft completion. The base address <c>http://bot</c> is resolved by
/// Aspire service discovery.
/// </summary>
public class BotNotifier(HttpClient http, ILogger<BotNotifier> logger)
{
    public async Task NotifyGameReadyAsync(Guid gameId)
    {
        try
        {
            var response = await http.PostAsJsonAsync("/internal/game-ready", new { gameId });
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify bot that game {GameId} is ready", gameId);
        }
    }
}
