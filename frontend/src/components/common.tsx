import type { ReactNode } from 'react';
import type { HoldStatus } from '../api/types';
import { AlertIcon, CheckIcon, InfoIcon } from './icons';

const ALERT_ICONS = { error: AlertIcon, success: CheckIcon, info: InfoIcon };

export function Alert({ kind, children }: { kind: 'error' | 'success' | 'info'; children: ReactNode }) {
  const Icon = ALERT_ICONS[kind];
  return (
    <div className={`alert alert-${kind}`} role={kind === 'error' ? 'alert' : 'status'}>
      <Icon className="alert-icon" />
      <div className="alert-body">{children}</div>
    </div>
  );
}

const STATUS_LABELS: Record<HoldStatus, string> = {
  HELD: 'অপেক্ষমাণ',
  CONFIRMED: 'নিশ্চিত',
  EXPIRED: 'মেয়াদ শেষ',
  CANCELLED: 'বাতিল',
};

export function StatusBadge({ status }: { status: HoldStatus }) {
  return (
    <span className={`badge badge-${status.toLowerCase()}`}>
      <span className="badge-dot" aria-hidden="true" />
      {STATUS_LABELS[status]} ({status})
    </span>
  );
}

export function Spinner({ label = 'লোড হচ্ছে…' }: { label?: string }) {
  return (
    <div className="loading" role="status">
      <span className="spinner" aria-hidden="true" />
      {label}
    </div>
  );
}

export function PageHeader({ title, subtitle, icon }: { title: string; subtitle?: ReactNode; icon?: ReactNode }) {
  return (
    <div className="page-header">
      {icon && <div className="page-icon">{icon}</div>}
      <div>
        <h1>{title}</h1>
        {subtitle && <p className="page-subtitle">{subtitle}</p>}
      </div>
    </div>
  );
}

/** Stacked bar: confirmed (green) + held (amber) + free (track). */
export function SeatBar({ total, held, confirmed }: { total: number; held: number; confirmed: number }) {
  const pct = (n: number) => (total > 0 ? Math.min(100, (n / total) * 100) : 0);
  return (
    <div
      className="seat-bar"
      role="img"
      aria-label={`${total}টি আসনের মধ্যে ${confirmed}টি নিশ্চিত, ${held}টি hold-এ`}
    >
      <span className="seat-bar-confirmed" style={{ width: `${pct(confirmed)}%` }} />
      <span className="seat-bar-held" style={{ width: `${pct(held)}%` }} />
    </div>
  );
}
