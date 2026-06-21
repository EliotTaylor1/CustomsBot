using System.Security.Claims;
using System.Text.Json;
using AspNet.Security.OAuth.Discord;
using CustomsBot.Data;
using CustomsBot.Riot;
using CustomsBot.Server.Auth;
using CustomsBot.Server.Draft;
using CustomsBot.Server.Lobby;
using CustomsBot.Server.Stats;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<CustomsBotDbContext>("customsbotdb");
builder.AddRiotClients();
builder.AddDiscordAuth();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services
    .AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddSingleton<LobbyService>();
builder.Services.AddSingleton<ChampionCatalog>();
builder.Services.AddSingleton<DraftService>();

builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<StatsQueryService>();

// Typed client to the bot's internal endpoint; "bot" is resolved by service discovery.
builder.Services.AddHttpClient<BotNotifier>(client => client.BaseAddress = new Uri("http://bot"));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// Discord OAuth login/logout + the current viewer.
app.MapGet("/auth/login", (string? returnUrl) =>
    Results.Challenge(new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
        [DiscordAuthenticationDefaults.AuthenticationScheme]));

app.MapGet("/auth/logout", async (HttpContext ctx, string? returnUrl) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect(returnUrl ?? "/");
});

app.MapGet("/auth/me", (ClaimsPrincipal user) => Results.Ok(new
{
    id = user.FindFirstValue(ClaimTypes.NameIdentifier),
    username = user.Identity?.Name,
})).RequireAuthorization();

// Everything under /api requires a logged-in Discord user (machine endpoints opt out below).
var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("series", async (ClaimsPrincipal user, LobbyService lobbies, CancellationToken ct) =>
    await lobbies.ListSeriesAsync(user.GuildIds(), ct));

api.MapPost("series/{seriesId:guid}/lobby", async (Guid seriesId, ClaimsPrincipal user, LobbyService lobbies, CancellationToken ct) =>
{
    var state = await lobbies.CreateLobbyAsync(seriesId, user.GuildIds(), ct);
    return state is null ? Results.NotFound() : Results.Ok(state);
});

api.MapGet("champions", async (ChampionCatalog catalog, CancellationToken ct) =>
    await catalog.GetAllAsync(ct));

// Production: Tournament-V5 posts here on game end. Locates the game by tournament code.
api.MapPost("tournament-callback", async (TournamentCallback cb, StatsService stats, CustomsBotDbContext db, CancellationToken ct) =>
{
    var game = await db.Games.FirstOrDefaultAsync(g => g.TournamentCode == cb.ShortCode, ct);
    if (game is null)
        return Results.NotFound();
    var result = await stats.RecordResultAsync(game.Id, $"{cb.Region}_{cb.GameId}", ct);
    return ToHttp(result);
}).AllowAnonymous();

// Dev / pull-by-code fallback: submit a known Match-V5 id for a game.
api.MapPost("games/{gameId:guid}/result", async (Guid gameId, SubmitResultRequest body, StatsService stats, CancellationToken ct) =>
    ToHttp(await stats.RecordResultAsync(gameId, body.MatchId, ct))).AllowAnonymous();

// Stats app read model (§5h), scoped to the viewer's guilds.
var stats = api.MapGroup("stats");
stats.MapGet("series", async (string? q, DateTimeOffset? from, DateTimeOffset? to, ClaimsPrincipal user, StatsQueryService s, CancellationToken ct) =>
    await s.SearchSeriesAsync(user.GuildIds(), q, from, to, ct));
stats.MapGet("players", async (string? q, ClaimsPrincipal user, StatsQueryService s, CancellationToken ct) =>
    await s.SearchPlayersAsync(user.GuildIds(), q, ct));
stats.MapGet("series/{id:guid}", async (Guid id, ClaimsPrincipal user, StatsQueryService s, CancellationToken ct) =>
    await s.GetSeriesDetailAsync(id, user.GuildIds(), ct) is { } d ? Results.Ok(d) : Results.NotFound());
stats.MapGet("games/{id:guid}", async (Guid id, ClaimsPrincipal user, StatsQueryService s, CancellationToken ct) =>
    await s.GetGameDetailAsync(id, user.GuildIds(), ct) is { } d ? Results.Ok(d) : Results.NotFound());
stats.MapGet("leaderboards/champions", async (ClaimsPrincipal user, StatsQueryService s, CancellationToken ct) =>
    await s.ChampionLeaderboardAsync(user.GuildIds(), ct));
stats.MapGet("leaderboards/players", async (ClaimsPrincipal user, StatsQueryService s, CancellationToken ct) =>
    await s.PlayerLeaderboardAsync(user.GuildIds(), ct));

app.MapHub<LobbyHub>("/hubs/lobby").RequireAuthorization();
app.MapHub<DraftHub>("/hubs/draft").RequireAuthorization();

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();

static IResult ToHttp(RecordResult result) => result switch
{
    RecordResult.Success or RecordResult.AlreadyComplete => Results.Ok(),
    RecordResult.GameNotFound => Results.NotFound(),
    RecordResult.MatchNotFound => Results.NotFound("Match not found for that id."),
    RecordResult.Invalid => Results.BadRequest("Game is not ready for a result."),
    _ => Results.Problem()
};
