using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CustomsBot.Server.Auth;

/// <summary>
/// Discord OAuth2 login (cookie session). The <c>guilds</c> scope lets us read which servers
/// the viewer belongs to; each membership is stored as a <see cref="GuildClaimType"/> claim so
/// every read query can be scoped to "series in servers you're actually in".
/// </summary>
public static class DiscordAuth
{
    public const string GuildClaimType = "guild";

    public static void AddDiscordAuth(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration.GetSection("Discord");

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = DiscordAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                // SPA: answer unauthenticated API/hub calls with a status code, never an HTML redirect.
                options.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddDiscord(options =>
            {
                options.ClientId = config["ClientId"] ?? "";
                options.ClientSecret = config["ClientSecret"] ?? "";
                options.Scope.Add("guilds");
                options.Events.OnCreatingTicket = async ctx =>
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me/guilds");
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
                    using var res = await ctx.Backchannel.SendAsync(req, ctx.HttpContext.RequestAborted);
                    if (!res.IsSuccessStatusCode)
                        return; // Login still succeeds; the viewer just sees no series until guilds resolve.

                    using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ctx.HttpContext.RequestAborted));
                    foreach (var guild in doc.RootElement.EnumerateArray())
                        if (guild.TryGetProperty("id", out var id) && id.GetString() is { } gid)
                            ctx.Identity!.AddClaim(new Claim(GuildClaimType, gid));
                };
            });

        builder.Services.AddAuthorization();
    }

    /// <summary>Discord guild ids the authenticated viewer is a member of.</summary>
    public static IReadOnlyList<ulong> GuildIds(this ClaimsPrincipal user) =>
        user.FindAll(GuildClaimType)
            .Select(c => ulong.TryParse(c.Value, out var id) ? id : 0UL)
            .Where(id => id != 0)
            .ToList();
}
