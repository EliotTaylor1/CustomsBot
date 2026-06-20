import { useEffect, useState } from 'react';
import { Landing } from './Landing';
import { Lobby } from './Lobby';
import { Draft } from './Draft';
import { Stats } from './Stats';
import { SeriesReview } from './SeriesReview';
import { GameDetail } from './GameDetail';
import './lobby.css';
import './draft.css';
import './stats.css';

type Route =
  | { name: 'lobby'; gameId: string }
  | { name: 'draft'; gameId: string }
  | { name: 'series'; seriesId: string }
  | { name: 'game'; gameId: string }
  | { name: 'stats' }
  | { name: 'landing' };

function parseRoute(): Route {
  const h = window.location.hash;
  const lobby = h.match(/^#\/lobby\/([0-9a-fA-F-]+)/);
  if (lobby) return { name: 'lobby', gameId: lobby[1] };
  const draft = h.match(/^#\/draft\/([0-9a-fA-F-]+)/);
  if (draft) return { name: 'draft', gameId: draft[1] };
  const series = h.match(/^#\/series\/([0-9a-fA-F-]+)/);
  if (series) return { name: 'series', seriesId: series[1] };
  const game = h.match(/^#\/game\/([0-9a-fA-F-]+)/);
  if (game) return { name: 'game', gameId: game[1] };
  if (h.startsWith('#/stats')) return { name: 'stats' };
  return { name: 'landing' };
}

function App() {
  const [route, setRoute] = useState<Route>(parseRoute());

  useEffect(() => {
    const onHash = () => setRoute(parseRoute());
    window.addEventListener('hashchange', onHash);
    return () => window.removeEventListener('hashchange', onHash);
  }, []);

  if (route.name === 'lobby') return <Lobby gameId={route.gameId} />;
  if (route.name === 'draft') return <Draft gameId={route.gameId} />;
  if (route.name === 'series') return <SeriesReview seriesId={route.seriesId} />;
  if (route.name === 'game') return <GameDetail gameId={route.gameId} />;
  if (route.name === 'stats') return <Stats />;
  return <Landing />;
}

export default App;
