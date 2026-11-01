import type { Hold, HoldStatus } from '../api/types';

/** Milliseconds left before the hold expires (never negative). */
export function remainingMs(hold: Pick<Hold, 'expiresAt'>, now: number): number {
  return Math.max(0, Date.parse(hold.expiresAt) - now);
}

/**
 * The status to show right now. A HELD hold whose time is up is shown as EXPIRED immediately, without waiting
 * for the next server refresh (rule 9).
 */
export function effectiveStatus(hold: Pick<Hold, 'status' | 'expiresAt'>, now: number): HoldStatus {
  return hold.status === 'HELD' && remainingMs(hold, now) === 0 ? 'EXPIRED' : hold.status;
}

/** "9:05" for 9 minutes 5 seconds, "1:02:03" above an hour. */
export function formatRemaining(ms: number): string {
  const total = Math.ceil(ms / 1000);
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const ss = String(s).padStart(2, '0');
  return h > 0 ? `${h}:${String(m).padStart(2, '0')}:${ss}` : `${m}:${ss}`;
}

export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
}
