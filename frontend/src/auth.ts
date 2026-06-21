import { useEffect, useState } from 'react';

export interface User {
  id: string;
  username: string;
}

/** Current Discord viewer, or null if not logged in. */
export function useAuth() {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch('/auth/me')
      .then((r) => (r.ok ? r.json() : null))
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setLoading(false));
  }, []);

  return { user, loading };
}

/** Send the viewer through Discord OAuth, returning to the current page afterwards. */
export function login() {
  const returnUrl = window.location.pathname + window.location.search + window.location.hash;
  window.location.href = `/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}

export function logout() {
  window.location.href = '/auth/logout';
}
