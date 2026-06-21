import { useEffect, useRef, useState } from 'react';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import type { LobbyPlayer, LobbyState } from './types';
import { ROLES } from './types';
import type { User } from './auth';

const SIDES: LobbyPlayer['side'][] = ['blue', 'red', 'spectator'];

export function Lobby({ gameId, user }: { gameId: string; user: User }) {
  const [state, setState] = useState<LobbyState | null>(null);
  const [connected, setConnected] = useState(false);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/lobby')
      .withAutomaticReconnect()
      .build();
    connectionRef.current = connection;

    connection.on('LobbyUpdated', (s: LobbyState) => setState(s));

    connection
      .start()
      .then(() => {
        setConnected(true);
        return connection.invoke('JoinLobby', gameId);
      })
      .catch((e) => console.error('Lobby connection failed', e));

    return () => {
      connection.stop();
      connectionRef.current = null;
    };
  }, [gameId]);

  const send = (method: string, ...args: unknown[]) => {
    const c = connectionRef.current;
    if (c && c.state === HubConnectionState.Connected) c.invoke(method, gameId, ...args).catch(console.error);
  };

  if (!state) {
    return <div className="page"><p className="muted">{connected ? 'Loading lobby…' : 'Connecting…'}</p></div>;
  }

  // Identity drives what the viewer can do: own role/ready always; sides/rename/start need owner or captain.
  const me = state.players.find((p) => p.discordId === user.id);
  const myPlayerId = me?.playerId ?? null;
  const canManage = user.id === state.ownerDiscordId || !!me?.isCaptain;

  const column = (side: LobbyPlayer['side']) => state.players.filter((p) => p.side === side);

  return (
    <div className="page">
      <header className="page-header">
        <a href="#/">← Series</a>
        <h1>{state.seriesName} — Game {state.gameNumber}</h1>
      </header>

      {state.started ? (
        <p className="banner">
          Champ select started — <a href={`#/draft/${state.gameId}`}>go to champ select →</a>
        </p>
      ) : canManage ? (
        <button className="start-btn" disabled={!state.canStart} onClick={() => send('StartChampSelect')}>
          {state.canStart ? 'Start champ select' : 'Start champ select (all team players must be ready)'}
        </button>
      ) : (
        <p className="muted">Waiting for the series owner or a captain to start champ select…</p>
      )}

      <div className="columns">
        <TeamColumn
          title={state.blueTeamName}
          side="blue"
          editableName={canManage}
          players={column('blue')}
          onRename={(name) => send('SetTeamName', 'blue', name)}
          send={send}
          disabled={state.started}
          canManage={canManage}
          myPlayerId={myPlayerId}
        />
        <TeamColumn
          title={state.redTeamName}
          side="red"
          editableName={canManage}
          players={column('red')}
          onRename={(name) => send('SetTeamName', 'red', name)}
          send={send}
          disabled={state.started}
          canManage={canManage}
          myPlayerId={myPlayerId}
        />
        <TeamColumn
          title="Spectators"
          side="spectator"
          players={column('spectator')}
          send={send}
          disabled={state.started}
          canManage={canManage}
          myPlayerId={myPlayerId}
        />
      </div>
    </div>
  );
}

function TeamColumn({
  title, side, players, send, disabled, editableName, onRename, canManage, myPlayerId,
}: {
  title: string;
  side: LobbyPlayer['side'];
  players: LobbyPlayer[];
  send: (method: string, ...args: unknown[]) => void;
  disabled: boolean;
  editableName?: boolean;
  onRename?: (name: string) => void;
  canManage: boolean;
  myPlayerId: string | null;
}) {
  return (
    <section className={`column column-${side}`}>
      {editableName ? (
        <input
          className="team-name"
          defaultValue={title}
          key={title}
          disabled={disabled}
          onBlur={(e) => e.target.value.trim() && e.target.value !== title && onRename?.(e.target.value.trim())}
        />
      ) : (
        <h2 className="team-name">{title}</h2>
      )}

      {players.length === 0 && <p className="muted">—</p>}

      {players.map((p) => {
        const isMe = p.playerId === myPlayerId;
        return (
          <div key={p.playerId} className="player-card">
            <div className="player-head">
              {p.avatar && <img src={p.avatar} alt="" className="avatar" />}
              <span>{p.username}{isMe && ' (you)'}</span>
              {p.isCaptain && <span className="badge" title="Team captain">captain</span>}
              {!p.hasPuuid && <span className="badge" title="Riot account not linked">no riot</span>}
            </div>

            {side !== 'spectator' && isMe && (
              <div className="player-controls">
                <select
                  value={p.role ?? ''}
                  disabled={disabled}
                  onChange={(e) => send('SetRole', p.playerId, e.target.value || null)}
                >
                  <option value="">role…</option>
                  {ROLES.map((r) => <option key={r} value={r}>{r}</option>)}
                </select>
                <label className="ready">
                  <input
                    type="checkbox"
                    checked={p.isReady}
                    disabled={disabled}
                    onChange={(e) => send('SetReady', p.playerId, e.target.checked)}
                  />
                  ready
                </label>
              </div>
            )}

            {side !== 'spectator' && !isMe && (
              <div className="player-controls">
                <span className="muted">{p.role ?? 'no role'}</span>
                {p.isReady && <span className="badge">ready</span>}
              </div>
            )}

            {(canManage || isMe) && (
              <div className="side-buttons">
                {SIDES.filter((s) => s !== side).map((s) => (
                  <button key={s} disabled={disabled} onClick={() => send('AssignSide', p.playerId, s)}>
                    → {s}
                  </button>
                ))}
              </div>
            )}
          </div>
        );
      })}
    </section>
  );
}
