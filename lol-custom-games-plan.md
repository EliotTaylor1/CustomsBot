# LoL Custom Games — Build Plan

Discord-driven League custom-game series: the bot sets up series and posts game codes; web apps handle drafting/champ select and stats. Orchestrated with .NET Aspire over Postgres.

---

## 1. Critical constraint — stats only come via the Tournament API

Ordinary custom games are not readable from the Riot API (privacy). The only supported path is **Tournament-V5**, which dictates the game lifecycle:

1. Register a **provider** once (region + callback URL).
2. Register a **tournament** (once per series, or reuse).
3. Generate a **single-use tournament code per game** (encodes map, spectator type, pick type, team size, allowed PUUIDs).
4. Players enter the code in-client to create the lobby.
5. On game end, Riot POSTs a callback with a `matchId` → pull full stats from **Match-V5** (fallback: pull end-of-game by code).

Implications baked into this plan:

- **Each player needs a Riot PUUID**, not just Discord details — codes are scoped by PUUID and stats are attributed by PUUID. Account-linking (Riot ID → PUUID via Account-V1) is required.
- **The champ-select web app is a *parallel* drafting tool, not the in-game draft.** It's the source of truth for what was agreed (and for fearless history/series records). The bot tells each player what to lock + hands them the code; Match-V5 fills in actual performance after.
- **Access gating is the main risk:** Tournament-V5 needs an approved Riot production app *plus* granted tournament access. **Build entirely against `tournament-stub-v5` with a dev key**; the real key swaps in at phases 6–7 with no architectural change.
- **Callback caveat:** prod callbacks require port 80/443 and a CA issued before 2012-01-29. In dev, use the stub + pull-by-code fallback.

---

## 2. Stack

| Concern | Choice | Notes |
|---|---|---|
| Orchestration | .NET Aspire (.NET 10 / C# 14) | AppHost wires resources + service discovery + Postgres. |
| Bot | C# + NetCord | Generic-Host integration fits Aspire. Pin an exact version — NetCord is `1.0.0-alpha`. |
| API | ASP.NET Core | REST + SignalR + Riot clients + tournament callback + EF Core/Npgsql. |
| Web | React + TypeScript (Vite) | Lobby, champ select, stats. REST + SignalR. |
| DB | Postgres | EF Core; migrations as a one-shot Aspire resource. |
| LoL static data | Data Dragon / Community Dragon | Champions, IDs, images, splash, music. No key. Cache by version. |
| Auth | Deferred | Lobby join is open for now. Discord OAuth later (closes slot impersonation — see §6). |

Champ-select music: free-use sources, non-commercial.

---

## 3. Architecture

```
AppHost (Aspire)
├── postgres        # container
├── migrations      # one-shot EF Core runner
├── Api (ASP.NET)   # REST + SignalR hubs + Riot clients + tournament callback
├── Bot (NetCord)   # slash commands, panels, posts codes & results
└── Web (React)     # lobby, champ select, stats
```

Shared libs: `Domain` (entities/enums), `Data` (DbContext + repos), `Riot` (Account-V1, Tournament-V5, Match-V5, Data Dragon clients).

**Bot ↔ API:** separate processes. On draft completion the API notifies the bot via a typed HTTP client to a small internal Bot endpoint (service discovery resolves it). No message broker.

---

## 4. Data model

- **Player** — `id`, `discord_id`, `discord_username`, `discord_avatar`, `riot_id`, `puuid`, `region`.
- **Series** — `id`, `name` (not unique), `guild_id`, `channel_id`, `draft_type`, `fearless` (bool), `map`, `region`, `best_of`, `status` (`setup`|`in_progress`|`complete`), `owner_discord_id`, `created_at`.
- **SeriesTeam** — `id`, `series_id`, `name` (default `Team: {captain}`), `captain_player_id`. Two per series. Persistent across games so best-of ("a team wins N") is well-defined even when sides swap.
- **SeriesParticipant** — `series_id`, `player_id`. Player pool.
- **Game** — `id`, `series_id`, `game_number`, `status` (`lobby`|`drafting`|`awaiting_result`|`complete`), `blue_team_id`/`red_team_id` (→`SeriesTeam`, side mapping this game), `tournament_code`, `riot_match_id`, `winner_team_id`, `completed_at`.
- **GamePlayer** — `game_id`, `player_id`, `side` (`blue`|`red`|`spectator`), `role`, `is_captain`, `is_ready`, `picked_champion_id`.
- **DraftAction** — `game_id`, `sequence`, `type` (`ban`|`pick`), `side`, `player_id` (null for bans), `champion_id`.
- **GamePlayerStats** — `game_id`, `player_id`, `champion_id`, `raw` (jsonb: full Match-V5 participant), promoted columns `kills`/`deaths`/`assists`/`gold`/`cs`/`damage`/`win`.

Status labels everywhere: `setup`=**Not started**, `in_progress`=**In progress**, `complete`=**Completed**.

Fearless exclusions are **derived, not stored**: unavailable in game *N* = champions *picked* in games `1..N-1` of the series. Picks lock globally; bans reset each game.

---

## 5. Behavior by component

### 5a. Bot — series setup
- `/create-series` options: **name**, **draft type**, **fearless** (on/off), **map**, **region**, **best-of**. Creates `Series` (`setup`) + two `SeriesTeam` rows, owned by the caller.
- **Player panel** (components): add players; capture Discord id/username/avatar → `Player` + `SeriesParticipant`.
- **Account-link prompt** for any player missing `puuid`: submit Riot ID → resolve via Account-V1 → store PUUID.

### 5b. Web — pregame lobby
- Assign pool into **blue/red/spectator**, set **team names**, assign **roles**, **ready check** per player.
- **Start champ select** unlocks only when all non-spectators ready → persist `GamePlayer`, set game `drafting`.
- Live-synced via SignalR `LobbyHub` (server-authoritative — see §5d).

### 5c. Web — champion select
Draft order (standard tournament draft):
- **Ban 1:** B R B R B R
- **Pick 1:** B | R R | B B | R
- **Ban 2:** R B R B
- **Pick 2:** R | B B | R

- Server-driven state machine; transitions broadcast over SignalR `DraftHub`.
- **Fearless on:** series-exclusion set greyed out/unpickable. **Off:** no cross-game exclusions.
- **Header:** series score (games won per `SeriesTeam`, e.g. `Team A 1–0 Team B`), team names from `SeriesTeam`, role-ordered slots, spectator view.
- **Swap** same-team picks (validate same side, re-broadcast).
- **Music** client-side.
- **On completion:** persist `DraftAction` + final `picked_champion_id`, set game `awaiting_result`, notify bot.

### 5d. Anti-tamper — server-authoritative (applies to lobby + draft)
- Server owns all state (phase, turn, available champions, picks, bans, exclusions, score) in memory + DB. Clients render what's pushed.
- **Clients send intents only** (`{pickChampion}`, `{banChampion}`, `{swap}`), never state.
- **Validate every intent:** correct turn, correct phase, connection owns the slot, champion legal (exists, not banned/picked, not fearless-locked). Reject → no change.
- **Slot-bound connections:** server issues a session token tied to one `GamePlayer` slot on join; checked per intent. **Spectators read-only.**
- **Expected-sequence number** on each intent rejects stale/duplicate/racing actions.
- Boundary: this stops illegal/out-of-turn/spectator actions. Slot *impersonation* (open lobby) is only fully closed by auth; interim mitigation = one-time slot-claim token so a claimed slot can't be hijacked mid-draft.

### 5e. Bot — game start
- On notify: generate the **tournament code** (map + pick type + spectator + allowed PUUIDs), post it to the channel **with each player's assigned champion**. Store code on `Game`.

### 5f. Post-game — stats capture
- Tournament callback (or pull-by-code) → Match-V5 → map participants to `Player` by PUUID → write `GamePlayerStats` (full `raw` jsonb + promoted columns).
- Set `Game.winner_team_id` (winning side → that game's `SeriesTeam`), game `complete`.
- **Re-evaluate series:** if a team reaches `floor(best_of/2)+1` wins, set series `complete` (auto-end). Bot may post a summary.

### 5g. Bot — series management
Targeted commands resolve the series via a **select menu** (label = name; description disambiguates duplicates: best-of · score · status · owner · short-ID). Owner-filtered for privileged commands; skip the menu if the caller owns exactly one eligible series.
- `/series list` — anyone. Active series (Not started + In progress): name, status, owner, draft type, fearless, region, score, short-ID.
- `/series new-draft` — owner, active only. Starts the next game's lobby.
- `/series edit` — owner, **not Completed only** (finished series are immutable). Edit **name** and/or **best-of** (odd; can't drop below `2×maxTeamWins−1`).
- `/series end` — owner. Ends early.
- `/series transfer-owner` — owner. Reassigns `owner_discord_id`.

### 5h. Web — stats
- **In-progress overview:** status + score, bans per game (`DraftAction`), per-player summary (champion/KDA/gold/CS/damage from promoted columns).
- **Per-game detail:** full Match-V5 set from `raw` jsonb (items, vision, damage breakdowns, objectives, runes, etc.).
- **Stats app:** search players/games/series by name or date; per-game view; series review; leaderboards (champion win rates, players) off promoted columns. Read-model queries + a few indexed views; no separate analytics store.

---

## 6. Build sequence (each phase has a verify check)

1. **Foundations** — AppHost + Postgres + ServiceDefaults + EF schema + migrations. → `aspire run` boots green; schema applies.
2. **Bot series setup** — `/create-series`, player panel. → creates `Series`/`SeriesTeam`; panel writes `Player`/`SeriesParticipant`.
3. **Account linking** — Account-V1 + link flow. → a player gets a valid PUUID.
4. **Pregame lobby** — React lobby + `LobbyHub`. → two browsers sync; Start needs all ready; `GamePlayer` persists.
5. **Champion select** — server-authoritative state machine, intent-only clients, slot binding, fearless, swap, spectators, score, music. → full draft completes; fearless greys earlier picks; forged out-of-turn/wrong-slot/spectator action rejected; `DraftAction` persists.
6. **Game start + code** — Tournament-V5 (stub) + bot posts code & champions. → bot message has a (stub) code and correct champion assignments.
7. **Stats capture** — callback + Match-V5 → full jsonb + promoted; winner→team; clinch eval. → completing a game writes full stats to the right players; series auto-completes exactly at `floor(best_of/2)+1` wins.
8. **Series management** — `list`/`new-draft`/`edit`/`end`/`transfer-owner`; owner enforcement; status in bot+web; edit blocked once Completed; duplicate-name menu. → non-owner blocked; duplicates distinguishable; Completed edit refused; status correct everywhere.
9. **Stats app** — search, per-game detail, series review, leaderboards. → search by name and date works; leaderboards aggregate across games.

Phases 1–5 deliver a usable drafting tool before tournament access lands; 6–7 light up on the real key.

---

## 7. Locked decisions & open interpretations

**Locked:** stub-first Tournament API · region per series · best-of with clinch auto-end · fearless as an independent on/off toggle (picks lock globally, bans reset per game) · spectators absent from stats · free-use music · open lobby auth · concurrent series (internal `id`, non-unique `name`, `/series list`) · owner-only edit/end/transfer + transferable · status shown in bot+web · edit only while not Completed · full Match-V5 payload stored & shown · server-authoritative draft/lobby.

**Two interpretations to confirm:**
- *Editing by name* — implemented as pick-from-menu (names aren't unique → resolves to hidden `id`); edit also lets you rename. If you meant a raw name argument, small swap.
- *Persistent teams* — best-of requires teams that persist as sides swap, so `SeriesTeam` is series-level and each game maps sides to it. This shifts "draft teams every pregame" to "teams are series-level; lobby assigns sides/roles." If teams should be redrawn each game, best-of needs a different "same team" rule.
