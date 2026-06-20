import { useEffect, useState } from 'react';
import { useChampions } from './champions';
import type { GameSummary, SeriesDetail } from './types';

export function SeriesReview({ seriesId }: { seriesId: string }) {
  const champions = useChampions();
  const [detail, setDetail] = useState<SeriesDetail | null>(null);

  useEffect(() => {
    fetch(`/api/stats/series/${seriesId}`).then((r) => (r.ok ? r.json() : null)).then(setDetail).catch(() => {});
  }, [seriesId]);

  if (!detail) return <div className="page"><p className="muted">Loading…</p></div>;
  const champName = (id: number | null) => (id == null ? '—' : champions?.get(id)?.name ?? `#${id}`);

  return (
    <div className="page">
      <header className="page-header">
        <a href="#/stats">← Stats</a>
        <h1>{detail.name}</h1>
      </header>

      <p>
        <strong>{detail.status}</strong> · Bo{detail.bestOf} · {detail.region} · fearless {detail.fearless ? 'on' : 'off'}
      </p>
      <p className="score-line">
        {detail.teams.map((t, i) => (
          <span key={t.teamId}>{i > 0 && ' — '}{t.name} <strong>{t.wins}</strong></span>
        ))}
      </p>

      {detail.games.map((g) => (
        <GameBlock key={g.id} game={g} champName={champName} />
      ))}
    </div>
  );
}

function GameBlock({ game, champName }: { game: GameSummary; champName: (id: number | null) => string }) {
  return (
    <section className="game-block">
      <h2>
        <a href={`#/game/${game.id}`}>Game {game.gameNumber}</a>
        <span className="muted"> · {game.status}{game.winnerTeamName ? ` · winner: ${game.winnerTeamName}` : ''}</span>
      </h2>

      <div className="bans">
        <span className="muted">Bans —</span> 🔵 {game.blueBans.map(champName).join(', ') || '—'} · 🔴 {game.redBans.map(champName).join(', ') || '—'}
      </div>

      <table className="stats-table">
        <thead>
          <tr><th>Player</th><th>Side</th><th>Role</th><th>Champion</th><th>K/D/A</th><th>Gold</th><th>CS</th><th>Dmg</th></tr>
        </thead>
        <tbody>
          {game.players.map((p) => (
            <tr key={p.playerId} className={p.win === true ? 'win' : p.win === false ? 'loss' : ''}>
              <td>{p.username}</td>
              <td>{p.side}</td>
              <td>{p.role ?? '—'}</td>
              <td>{champName(p.championId)}</td>
              <td>{p.kills != null ? `${p.kills}/${p.deaths}/${p.assists}` : '—'}</td>
              <td>{p.gold ?? '—'}</td>
              <td>{p.cs ?? '—'}</td>
              <td>{p.damage ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
