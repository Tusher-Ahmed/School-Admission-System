import type { ReactNode } from 'react';
import type { HoldStatus } from '../api/types';

export function Alert({ kind, children }: { kind: 'error' | 'success' | 'info'; children: ReactNode }) {
  return (
    <div className={`alert alert-${kind}`} role={kind === 'error' ? 'alert' : 'status'}>
      {children}
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
      {STATUS_LABELS[status]} ({status})
    </span>
  );
}
