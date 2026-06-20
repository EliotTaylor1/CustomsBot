import { useEffect, useState } from 'react';
import { useChampions } from './champions';
import type { GameDetail as GameDetailDto } from './types';

export function GameDetail({ gameId }: { gameId: string }) {
  const champions = useChampions();
  const [detail, setDetail] = useState<GameDetailDto | null>(null);

  useEffect(() => {
    fetch(`/api/stats/games/${gameId}`).then((r) => (r.ok ? r.json() : null)).then(setDetail).catch(() => {});
  }, [gameId]);

  if (!detail) return <div className="page"><p className="muted">Loading…</p></div>;
  const champName = (id: number | null) => (id == null ? '—' : champions?.get(id)?.name ?? `#${id}`);
  const { summary } = detail;

  return (
    <div className="page">
      <header className="page-header">
        <a href="#/stats">← Stats</a>
        <h1>Game {summary.gameNumber}</h1>
      </header>
      <p className="muted">
        {summary.blueTeamName} vs {summary.redTeamName} · {summary.status}
        {summary.winnerTeamName ? ` · winner: ${summary.winnerTeamName}` : ''}
      </p>

      <table className="stats-table">
        <thead>
          <tr><th>Player</th><th>Side</th><th>Champion</th><th>K/D/A</th><th>Gold</th><th>CS</th><th>Dmg</th></tr>
        </thead>
        <tbody>
          {summary.players.map((p) => (
            <tr key={p.playerId} className={p.win === true ? 'win' : p.win === false ? 'loss' : ''}>
              <td>{p.username}</td><td>{p.side}</td><td>{champName(p.championId)}</td>
              <td>{p.kills != null ? `${p.kills}/${p.deaths}/${p.assists}` : '—'}</td>
              <td>{p.gold ?? '—'}</td><td>{p.cs ?? '—'}</td><td>{p.damage ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <h2>Full match data</h2>
      {detail.rawParticipants.length === 0 && <p className="muted">No match data captured yet.</p>}
      {detail.rawParticipants.map((p, i) => (
        <details key={i} className="raw">
          <summary>
            {String(p['riotIdGameName'] ?? p['summonerName'] ?? `Participant ${i + 1}`)} — {String(p['championName'] ?? '')}
          </summary>
          <pre>{JSON.stringify(p, null, 2)}</pre>
        </details>
      ))}
    </div>
  );
}
