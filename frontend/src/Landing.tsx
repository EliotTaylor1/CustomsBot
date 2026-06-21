import { useEffect, useState } from 'react';
import type { SeriesSummary } from './types';
import { logout } from './auth';

export function Landing() {
  const [series, setSeries] = useState<SeriesSummary[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch('/api/series');
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      setSeries(await res.json());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load series');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const openLobby = async (seriesId: string) => {
    const res = await fetch(`/api/series/${seriesId}/lobby`, { method: 'POST' });
    if (!res.ok) {
      setError(`Couldn't open lobby (HTTP ${res.status})`);
      return;
    }
    const state = await res.json();
    window.location.hash = `#/lobby/${state.gameId}`;
  };

  return (
    <div className="page">
      <header className="page-header">
        <h1>Custom Games</h1>
        <a href="#/stats">Stats →</a>
        <button onClick={load} disabled={loading}>{loading ? 'Loading…' : 'Refresh'}</button>
        <button onClick={logout}>Log out</button>
      </header>

      {error && <p className="error">{error}</p>}

      {series.length === 0 && !loading && (
        <p className="muted">No series in your servers yet. Create one with <code>/create-series</code> in Discord.</p>
      )}

      <ul className="series-list">
        {series.map((s) => (
          <li key={s.id} className="series-row">
            <div>
              <strong>{s.name}</strong>
              <span className="muted"> · Bo{s.bestOf} · {s.status} · {s.participantCount} players</span>
            </div>
            <button onClick={() => openLobby(s.id)}>Open lobby</button>
          </li>
        ))}
      </ul>
    </div>
  );
}
