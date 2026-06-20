# CustomsBot

Discord-driven League of Legends custom-game series. The bot sets up series and posts tournament codes; the web app handles drafting, champion select, and stats. Orchestrated with .NET Aspire over Postgres.

## Stack

- .NET Aspire (.NET 10 / C# 14)
- NetCord (Discord bot)
- ASP.NET Core (REST + SignalR) with EF Core / Npgsql
- React + TypeScript (Vite)
- Postgres

## Project structure

| Project | Role |
|---|---|
| `CustomsBot.AppHost` | Aspire orchestration: wires Postgres, migrations, bot, API, frontend |
| `CustomsBot.ServiceDefaults` | Shared OpenTelemetry, health checks, service discovery |
| `CustomsBot.Domain` | Entities and enums |
| `CustomsBot.Data` | EF Core `DbContext`, configuration, migrations |
| `CustomsBot.MigrationService` | One-shot worker that applies migrations on startup |
| `CustomsBot.Riot` | Riot clients: Account-V1, Match-V5, Tournament-V5 (stub), Data Dragon |
| `CustomsBot.Server` | API: lobby + draft SignalR hubs, tournament callback, stats endpoints |
| `CustomsBot.Bot` | Discord bot: slash commands, panels, posts codes and results |
| `frontend` | Web app: lobby, champion select, stats |

## Prerequisites

- .NET 10 SDK
- Docker (for the Postgres container)
- Node.js 20.19+ or 22.12+ (for the frontend)
- A Discord bot token
- A Riot API key

## Configuration

Secrets are supplied to the AppHost as parameters and are never committed. Set them with user-secrets:

```bash
dotnet user-secrets set "Parameters:discord-token" "<discord bot token>" --project CustomsBot.AppHost
dotnet user-secrets set "Parameters:riot-api-key" "<riot api key>" --project CustomsBot.AppHost
```

The AppHost injects these into the bot and API at runtime.

To switch Tournament-V5 from stub to production, set `Riot:Tournament:UseStub` to `false` and configure `Riot:Tournament:CallbackUrl` (and region) for the bot.

## Running

```bash
dotnet run --project CustomsBot.AppHost
```

This starts the Aspire dashboard and brings up Postgres, the migration runner, the API, the bot, and the frontend. Open the dashboard URL from the console to reach each resource, including the web app.

## Using the bot

1. `/create-series` - create a series (name, draft type, fearless on/off, map, region, best-of). Posts a panel; use its menu to add players from the server.
2. `/link-account riot-id:<gameName#tagLine> region:<region>` - each player links their Riot account so they can be issued tournament codes.
3. `/series new-draft` - opens the next game's lobby (link to the web app).
4. In the web lobby: assign players to blue/red/spectator, set roles, ready up, then start champion select.
5. Champion select runs in the browser (server-authoritative draft). On completion the bot posts the tournament code with each player's champion.
6. After the game, results arrive via the Tournament callback (production) or are submitted manually for development.

Other commands:

- `/series list` - active series in the server.
- `/series edit` - change name and/or best-of (owner only, not completed).
- `/series end` - end a series early (owner only).
- `/series transfer-owner` - reassign ownership (owner only).

Commands that target a series show a select menu when you own more than one eligible series.

## Web app

- Landing: list series and open a lobby.
- Lobby: side/role assignment and ready check.
- Champion select: tournament draft with bans, picks, swaps, and fearless exclusions.
- Stats: search series and players, per-series and per-game detail, leaderboards.
