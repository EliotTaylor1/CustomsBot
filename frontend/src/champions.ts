import { useEffect, useState } from 'react';
import type { Champion } from './types';

let cache: Promise<Map<number, Champion>> | null = null;

export function loadChampions(): Promise<Map<number, Champion>> {
  if (!cache) {
    cache = fetch('/api/champions')
      .then((r) => r.json())
      .then((cs: Champion[]) => new Map(cs.map((c) => [c.id, c])));
  }
  return cache;
}

export function useChampions(): Map<number, Champion> | null {
  const [map, setMap] = useState<Map<number, Champion> | null>(null);
  useEffect(() => {
    loadChampions().then(setMap).catch(() => setMap(new Map()));
  }, []);
  return map;
}
