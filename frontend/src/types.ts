export interface LobbyPlayer {
  playerId: string;
  username: string;
  avatar: string | null;
  hasPuuid: boolean;
  side: 'blue' | 'red' | 'spectator';
  role: string | null;
  isReady: boolean;
}

export interface LobbyState {
  gameId: string;
  seriesId: string;
  seriesName: string;
  gameNumber: number;
  blueTeamName: string;
  redTeamName: string;
  players: LobbyPlayer[];
  canStart: boolean;
  started: boolean;
  sequence: number;
}

export interface SeriesSummary {
  id: string;
  name: string;
  status: string;
  bestOf: number;
  participantCount: number;
}

export const ROLES = ['Top', 'Jungle', 'Mid', 'Bot', 'Support'] as const;

export interface Champion {
  id: number;
  name: string;
  imageUrl: string;
}

export interface DraftSlot {
  slotId: string;
  playerId: string;
  username: string;
  role: string | null;
  championId: number | null;
  isCurrentPick: boolean;
  claimed: boolean;
}

export interface DraftState {
  gameId: string;
  seriesId: string;
  blueTeamName: string;
  redTeamName: string;
  blueScore: number;
  redScore: number;
  phase: 'ban' | 'pick' | 'complete';
  currentSide: 'blue' | 'red' | null;
  stepIndex: number;
  totalSteps: number;
  blueSlots: DraftSlot[];
  redSlots: DraftSlot[];
  blueBans: number[];
  redBans: number[];
  fearlessExcluded: number[];
  complete: boolean;
  sequence: number;
}

export interface ClaimResult {
  token: string;
  slotId: string;
  state: DraftState;
}

export interface SeriesSearchRow {
  id: string;
  name: string;
  status: string;
  bestOf: number;
  participantCount: number;
  createdAt: string;
}

export interface PlayerSearchRow {
  id: string;
  username: string;
  riotId: string | null;
  region: string | null;
}

export interface TeamScore {
  teamId: string;
  name: string;
  wins: number;
}

export interface PlayerLine {
  playerId: string;
  username: string;
  side: string;
  role: string | null;
  championId: number | null;
  kills: number | null;
  deaths: number | null;
  assists: number | null;
  gold: number | null;
  cs: number | null;
  damage: number | null;
  win: boolean | null;
}

export interface GameSummary {
  id: string;
  gameNumber: number;
  status: string;
  blueTeamName: string | null;
  redTeamName: string | null;
  winnerTeamName: string | null;
  blueBans: number[];
  redBans: number[];
  players: PlayerLine[];
}

export interface SeriesDetail {
  id: string;
  name: string;
  status: string;
  bestOf: number;
  fearless: boolean;
  region: string;
  teams: TeamScore[];
  games: GameSummary[];
}

export interface GameDetail {
  summary: GameSummary;
  rawParticipants: Record<string, unknown>[];
}

export interface ChampionLeaderboardRow {
  championId: number;
  games: number;
  wins: number;
  winRate: number;
}

export interface PlayerLeaderboardRow {
  playerId: string;
  username: string;
  games: number;
  wins: number;
  winRate: number;
  kills: number;
  deaths: number;
  assists: number;
  kda: number;
}
