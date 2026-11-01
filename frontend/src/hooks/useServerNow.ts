import { useEffect, useState } from 'react';
import { serverNow } from '../api/client';

/** Current server time in ms, updated every second (drives the countdowns). */
export function useServerNow(intervalMs = 1000): number {
  const [now, setNow] = useState(serverNow);
  useEffect(() => {
    const timer = window.setInterval(() => setNow(serverNow()), intervalMs);
    return () => window.clearInterval(timer);
  }, [intervalMs]);
  return now;
}
