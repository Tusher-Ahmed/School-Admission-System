import { useCallback, useEffect, useRef, useState } from 'react';

interface PollingState<T> {
  data: T | null;
  error: unknown;
  loading: boolean;
  reload: () => Promise<void>;
}

/**
 * Calls `load` now and then every `intervalMs`. Keeps the last good data when a refresh fails, so a short
 * server outage shows an error message on top of the data instead of a blank page.
 * Pass `load = null` to pause (e.g. no student ID yet).
 */
export function usePolling<T>(load: (() => Promise<T>) | null, intervalMs: number): PollingState<T> {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [loading, setLoading] = useState(load !== null);
  const loadRef = useRef(load);
  const inFlight = useRef(false);

  useEffect(() => {
    loadRef.current = load;
  }, [load]);

  const reload = useCallback(async () => {
    const current = loadRef.current;
    if (!current || inFlight.current) return;
    inFlight.current = true;
    try {
      const result = await current();
      if (loadRef.current === current) {
        setData(result);
        setError(null);
      }
    } catch (e) {
      if (loadRef.current === current) setError(e);
    } finally {
      inFlight.current = false;
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    setData(null);
    setError(null);
    if (!load) {
      setLoading(false);
      return;
    }
    setLoading(true);
    void reload();
    const timer = window.setInterval(() => void reload(), intervalMs);
    return () => window.clearInterval(timer);
  }, [load, intervalMs, reload]);

  return { data, error, loading, reload };
}
