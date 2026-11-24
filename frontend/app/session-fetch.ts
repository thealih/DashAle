let refreshInFlight: Promise<boolean> | undefined;

async function refreshSession(): Promise<boolean> {
  async function refreshIfNeeded() {
    // Another tab may have renewed the shared cookies while this tab waited.
    if ((await fetch('/api/me', { cache: 'no-store' })).ok) return true;
    return (await fetch('/api/auth/refresh', { method: 'POST', cache: 'no-store' })).ok;
  }

  try {
    return navigator.locks
      ? await navigator.locks.request('pricetracker-session-refresh', refreshIfNeeded)
      : await refreshIfNeeded();
  } catch {
    return false;
  }
}

export async function sessionFetch(path: string, options?: RequestInit): Promise<Response> {
  const response = await fetch(path, options);
  if (response.status !== 401 || path.startsWith('/api/auth/')) return response;

  refreshInFlight ??= refreshSession().finally(() => { refreshInFlight = undefined; });
  return await refreshInFlight ? fetch(path, options) : response;
}
