import { useEffect, useState } from 'react';
import { useChampions } from './champions';
import type {
  ChampionLeaderboardRow, PlayerLeaderboardRow, PlayerSearchRow, SeriesSearchRow,
} from './types';

export function Stats() {
  const champions = useChampions();
  const [q, setQ] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [series, setSeries] = useState<SeriesSearchRow[]>([]);
  const [players, setPlayers] = useState<PlayerSearchRow[]>([]);
  const [champLb, setChampLb] = useState<ChampionLeaderboardRow[]>([]);
  const [playerLb, setPlayerLb] = useState<PlayerLeaderboardRow[]>([]);

  const search = async () => {
    const params = new URLSearchParams();
    if (q) params.set('q', q);
    if (from) params.set('from', new Date(from).toISOString());
    if (to) params.set('to', new Date(to).toISOString());
    const [s, p] = await Promise.all([
      fetch(`/api/stats/series?${params}`).then((r) => r.json()),
      fetch(`/api/stats/players?${q ? `q=${encodeURIComponent(q)}` : ''}`).then((r) => r.json()),
    ]);
    setSeries(s);
    setPlayers(p);
  };

  useEffect(() => {
    search();
    fetch('/api/stats/leaderboards/champions').then((r) => r.json()).then(setChampLb).catch(() => {});
    fetch('/api/stats/leaderboards/players').then((r) => r.json()).then(setPlayerLb).catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const champName = (id: number) => champions?.get(id)?.name ?? `#${id}`;

  return (
    <div className="page">
      <header className="page-header">
        <a href="#/">← Home</a>
        <h1>Stats</h1>
      </header>

      <div className="search-bar">
        <input placeholder="Search series / players…" value={q} onChange={(e) => setQ(e.target.value)} />
        <label>from <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></label>
        <label>to <input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></label>
        <button onClick={search}>Search</button>
      </div>

      <div className="stats-cols">
        <section>
          <h2>Series</h2>
          {series.length === 0 && <p className="muted">No matches.</p>}
          <ul className="plain">
            {series.map((s) => (
              <li key={s.id}>
                <a href={`#/series/${s.id}`}>{s.name}</a>
                <span className="muted"> · Bo{s.bestOf} · {s.status} · {new Date(s.createdAt).toLocaleDateString()}</span>
              </li>
            ))}
          </ul>
        </section>

        <section>
          <h2>Players</h2>
          {players.length === 0 && <p className="muted">No matches.</p>}
          <ul className="plain">
            {players.map((p) => (
              <li key={p.id}>{p.username} <span className="muted">{p.riotId ?? ''} {p.region ?? ''}</span></li>
            ))}
          </ul>
        </section>
      </div>

      <div className="stats-cols">
        <section>
          <h2>Champion leaderboard</h2>
          <table className="stats-table">
            <thead><tr><th>Champion</th><th>Games</th><th>Wins</th><th>Win%</th></tr></thead>
            <tbody>
              {champLb.slice(0, 25).map((c) => (
                <tr key={c.championId}>
                  <td>{champName(c.championId)}</td><td>{c.games}</td><td>{c.wins}</td>
                  <td>{(c.winRate * 100).toFixed(0)}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>

        <section>
          <h2>Player leaderboard</h2>
          <table className="stats-table">
            <thead><tr><th>Player</th><th>Games</th><th>Win%</th><th>KDA</th></tr></thead>
            <tbody>
              {playerLb.slice(0, 25).map((p) => (
                <tr key={p.playerId}>
                  <td>{p.username}</td><td>{p.games}</td>
                  <td>{(p.winRate * 100).toFixed(0)}%</td><td>{p.kda.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      </div>
    </div>
  );
}
