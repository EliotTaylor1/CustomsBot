using System.Text.Json;
using CustomsBot.Data;
using CustomsBot.Riot;
using CustomsBot.Server.Draft;
using CustomsBot.Server.Lobby;
using CustomsBot.Server.Stats;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<CustomsBotDbContext>("customsbotdb");
builder.AddRiotClients();

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

var api = app.MapGroup("/api");

api.MapGet("series", async (LobbyService lobbies, CancellationToken ct) =>
    await lobbies.ListSeriesAsync(ct));

api.MapPost("series/{seriesId:guid}/lobby", async (Guid seriesId, LobbyService lobbies, CancellationToken ct) =>
{
    var state = await lobbies.CreateLobbyAsync(seriesId, ct);
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
});

// Dev / pull-by-code fallback: submit a known Match-V5 id for a game.
api.MapPost("games/{gameId:guid}/result", async (Guid gameId, SubmitResultRequest body, StatsService stats, CancellationToken ct) =>
    ToHttp(await stats.RecordResultAsync(gameId, body.MatchId, ct)));

// Stats app read model (§5h).
var stats = api.MapGroup("stats");
stats.MapGet("series", async (string? q, DateTimeOffset? from, DateTimeOffset? to, StatsQueryService s, CancellationToken ct) =>
    await s.SearchSeriesAsync(q, from, to, ct));
stats.MapGet("players", async (string? q, StatsQueryService s, CancellationToken ct) =>
    await s.SearchPlayersAsync(q, ct));
stats.MapGet("series/{id:guid}", async (Guid id, StatsQueryService s, CancellationToken ct) =>
    await s.GetSeriesDetailAsync(id, ct) is { } d ? Results.Ok(d) : Results.NotFound());
stats.MapGet("games/{id:guid}", async (Guid id, StatsQueryService s, CancellationToken ct) =>
    await s.GetGameDetailAsync(id, ct) is { } d ? Results.Ok(d) : Results.NotFound());
stats.MapGet("leaderboards/champions", async (StatsQueryService s, CancellationToken ct) =>
    await s.ChampionLeaderboardAsync(ct));
stats.MapGet("leaderboards/players", async (StatsQueryService s, CancellationToken ct) =>
    await s.PlayerLeaderboardAsync(ct));

app.MapHub<LobbyHub>("/hubs/lobby");
app.MapHub<DraftHub>("/hubs/draft");

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
