import { useEffect, useMemo, useRef, useState } from 'react';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import type { Champion, ClaimResult, DraftSlot, DraftState } from './types';
import { MusicToggle } from './MusicToggle';

export function Draft({ gameId }: { gameId: string }) {
  const [champions, setChampions] = useState<Champion[]>([]);
  const [state, setState] = useState<DraftState | null>(null);
  const [selected, setSelected] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [swapFrom, setSwapFrom] = useState<string | null>(null);
  const [myToken, setMyToken] = useState<string | null>(() => sessionStorage.getItem(`draft-token-${gameId}`));
  const [mySlotId, setMySlotId] = useState<string | null>(() => sessionStorage.getItem(`draft-slot-${gameId}`));
  const connectionRef = useRef<HubConnection | null>(null);

  const byId = useMemo(() => new Map(champions.map((c) => [c.id, c])), [champions]);

  useEffect(() => {
    fetch('/api/champions').then((r) => r.json()).then(setChampions).catch(console.error);
  }, []);

  useEffect(() => {
    const connection = new HubConnectionBuilder().withUrl('/hubs/draft').withAutomaticReconnect().build();
    connectionRef.current = connection;
    connection.on('DraftUpdated', (s: DraftState) => setState(s));

    connection.start().then(async () => {
      await connection.invoke('JoinDraft', gameId);
      const storedToken = sessionStorage.getItem(`draft-token-${gameId}`);
      const storedSlot = sessionStorage.getItem(`draft-slot-${gameId}`);
      if (storedToken && storedSlot) {
        // Re-bind to a previously claimed slot after a reload/reconnect.
        const result: ClaimResult | null = await connection.invoke('ClaimSlot', gameId, storedSlot, storedToken);
        if (!result) {
          sessionStorage.removeItem(`draft-token-${gameId}`);
          sessionStorage.removeItem(`draft-slot-${gameId}`);
          setMyToken(null);
          setMySlotId(null);
        }
      }
    }).catch(console.error);

    return () => {
      connection.stop();
      connectionRef.current = null;
    };
  }, [gameId]);

  const invoke = (method: string, ...args: unknown[]) => {
    const c = connectionRef.current;
    if (c && c.state === HubConnectionState.Connected) return c.invoke(method, ...args);
    return Promise.reject(new Error('not connected'));
  };

  const claim = async (slotId: string) => {
    const result: ClaimResult | null = await invoke('ClaimSlot', gameId, slotId, null);
    if (result) {
      sessionStorage.setItem(`draft-token-${gameId}`, result.token);
      sessionStorage.setItem(`draft-slot-${gameId}`, result.slotId);
      setMyToken(result.token);
      setMySlotId(result.slotId);
    }
  };

  if (!state) return <div className="page"><p className="muted">Connecting to champ select…</p></div>;

  const mySide: 'blue' | 'red' | null =
    state.blueSlots.some((s) => s.slotId === mySlotId) ? 'blue'
      : state.redSlots.some((s) => s.slotId === mySlotId) ? 'red'
        : null;
  const isMyTurn = !state.complete && mySide !== null && state.currentSide === mySide;

  const picks = [...state.blueSlots, ...state.redSlots].map((s) => s.championId).filter((id): id is number => id !== null);
  const unavailable = new Set<number>([...state.blueBans, ...state.redBans, ...picks, ...state.fearlessExcluded]);

  const lockIn = () => {
    if (!isMyTurn || selected === null || !myToken) return;
    const method = state.phase === 'ban' ? 'Ban' : 'Pick';
    invoke(method, gameId, myToken, selected, state.sequence).then(() => setSelected(null)).catch(console.error);
  };

  const onSlotClick = (slot: DraftSlot) => {
    if (mySide === null || !myToken || slot.championId === null) return;
    const onMySide = (mySide === 'blue' ? state.blueSlots : state.redSlots).some((s) => s.slotId === slot.slotId);
    if (!onMySide) return;
    if (swapFrom === null) {
      setSwapFrom(slot.slotId);
    } else if (swapFrom === slot.slotId) {
      setSwapFrom(null);
    } else {
      invoke('Swap', gameId, myToken, swapFrom, slot.slotId, state.sequence).catch(console.error);
      setSwapFrom(null);
    }
  };

  const turnLabel = state.complete
    ? 'Draft complete'
    : `${state.currentSide === 'blue' ? state.blueTeamName : state.redTeamName} ${state.phase === 'ban' ? 'banning' : 'picking'}`;

  const filtered = champions.filter((c) => c.name.toLowerCase().includes(search.toLowerCase()));

  return (
    <div className="page draft">
      <header className="draft-header">
        <a href={`#/lobby/${gameId}`}>← Lobby</a>
        <div className="score">
          <span className="team-blue">{state.blueTeamName} {state.blueScore}</span>
          <span className="muted"> vs </span>
          <span className="team-red">{state.redScore} {state.redTeamName}</span>
        </div>
        <div className={`turn turn-${state.currentSide ?? 'none'}`}>{turnLabel}</div>
        <MusicToggle />
      </header>

      {mySide === null && !state.complete && (
        <p className="muted">Spectating — claim a slot below to take part.</p>
      )}

      <div className="draft-body">
        <TeamPanel
          title={state.blueTeamName} side="blue" slots={state.blueSlots} byId={byId}
          mySlotId={mySlotId} swapFrom={swapFrom} onClaim={claim} onSlotClick={onSlotClick}
        />

        <div className="center">
          <BanRow label="Blue bans" bans={state.blueBans} byId={byId} />
          <BanRow label="Red bans" bans={state.redBans} byId={byId} />

          {!state.complete && (
            <>
              <input className="champ-search" placeholder="Search champions…" value={search} onChange={(e) => setSearch(e.target.value)} />
              <div className="champ-grid">
                {filtered.map((c) => {
                  const blocked = unavailable.has(c.id);
                  return (
                    <button
                      key={c.id}
                      className={`champ ${blocked ? 'blocked' : ''} ${selected === c.id ? 'selected' : ''}`}
                      title={c.name}
                      disabled={blocked || !isMyTurn}
                      onClick={() => setSelected(c.id)}
                    >
                      <img src={c.imageUrl} alt={c.name} />
                    </button>
                  );
                })}
              </div>
              <button className="lock-in" disabled={!isMyTurn || selected === null} onClick={lockIn}>
                {isMyTurn ? `Lock in ${state.phase}` : 'Waiting for the other team…'}
              </button>
            </>
          )}

          {state.complete && <p className="banner">Draft complete — awaiting game result.</p>}
        </div>

        <TeamPanel
          title={state.redTeamName} side="red" slots={state.redSlots} byId={byId}
          mySlotId={mySlotId} swapFrom={swapFrom} onClaim={claim} onSlotClick={onSlotClick}
        />
      </div>
    </div>
  );
}

function TeamPanel({
  title, side, slots, byId, mySlotId, swapFrom, onClaim, onSlotClick,
}: {
  title: string;
  side: 'blue' | 'red';
  slots: DraftSlot[];
  byId: Map<number, Champion>;
  mySlotId: string | null;
  swapFrom: string | null;
  onClaim: (slotId: string) => void;
  onSlotClick: (slot: DraftSlot) => void;
}) {
  return (
    <section className={`team-panel team-${side}`}>
      <h2>{title}</h2>
      {slots.map((s) => {
        const champ = s.championId !== null ? byId.get(s.championId) : null;
        return (
          <div
            key={s.slotId}
            className={`slot ${s.isCurrentPick ? 'current' : ''} ${s.slotId === mySlotId ? 'mine' : ''} ${s.slotId === swapFrom ? 'swap-from' : ''}`}
            onClick={() => onSlotClick(s)}
          >
            {champ ? <img src={champ.imageUrl} alt={champ.name} className="slot-champ" /> : <div className="slot-champ empty" />}
            <div className="slot-meta">
              <span className="slot-role">{s.role ?? '—'}</span>
              <span className="slot-name">{s.username}</span>
            </div>
            {!s.claimed && s.slotId !== mySlotId && (
              <button className="claim" onClick={(e) => { e.stopPropagation(); onClaim(s.slotId); }}>Claim</button>
            )}
          </div>
        );
      })}
    </section>
  );
}

function BanRow({ label, bans, byId }: { label: string; bans: number[]; byId: Map<number, Champion> }) {
  return (
    <div className="ban-row">
      <span className="muted">{label}:</span>
      {bans.map((id, i) => {
        const c = byId.get(id);
        return c ? <img key={i} src={c.imageUrl} alt={c.name} title={c.name} className="ban" /> : null;
      })}
    </div>
  );
}
