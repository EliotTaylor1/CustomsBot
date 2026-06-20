using CustomsBot.Bot;
using CustomsBot.Data;
using CustomsBot.Riot;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<CustomsBotDbContext>("customsbotdb");
builder.AddRiotClients();
builder.Services.AddSingleton<TournamentCodeService>();
builder.Services.AddScoped<SeriesManager>();

builder.Services
    .AddDiscordGateway(options => options.Intents = GatewayIntents.Guilds)
    .AddApplicationCommands()
    .AddComponentInteractions<UserMenuInteraction, UserMenuInteractionContext>()
    .AddComponentInteractions<StringMenuInteraction, StringMenuInteractionContext>();

var app = builder.Build();

app.AddModules(typeof(Program).Assembly);

// Internal endpoint the API calls when a draft completes (service discovery resolves "bot").
app.MapPost("/internal/game-ready", GameReadyEndpoint.HandleAsync);

app.MapDefaultEndpoints();

await app.RunAsync();
