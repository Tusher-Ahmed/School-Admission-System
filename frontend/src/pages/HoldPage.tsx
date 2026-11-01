import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError, api, toUserMessage } from '../api/client';
import type { Hold, HoldStatus } from '../api/types';
import { Alert, Spinner, StatusBadge } from '../components/common';
import { CheckIcon, ClockIcon, FileIcon, WalletIcon, XIcon } from '../components/icons';
import { effectiveStatus, formatDateTime, formatRemaining, remainingMs } from '../holds/holdTime';
import { usePolling } from '../hooks/usePolling';
import { useServerNow } from '../hooks/useServerNow';
import { useStudent } from '../student/StudentContext';

const REFRESH_MS = 5000;

const STATUS_COPY: Record<HoldStatus, { title: string; text: string }> = {
  HELD: { title: 'আসনটি আপনার জন্য রাখা আছে', text: 'নির্ধারিত সময়ের মধ্যে ভর্তি ফি দিলে ভর্তি নিশ্চিত হবে।' },
  CONFIRMED: { title: 'অভিনন্দন! ভর্তি নিশ্চিত হয়েছে', text: 'ভর্তি ফি জমা হয়েছে। আসনটি এখন স্থায়ীভাবে আপনার।' },
  EXPIRED: { title: 'আবেদনের মেয়াদ শেষ', text: 'সময়ের মধ্যে ফি জমা হয়নি, তাই আসনটি অন্যদের জন্য খুলে দেওয়া হয়েছে।' },
  CANCELLED: { title: 'আবেদন বাতিল হয়েছে', text: 'আসনটি অন্যদের জন্য খুলে দেওয়া হয়েছে।' },
};

const STATUS_ICON: Record<HoldStatus, typeof ClockIcon> = {
  HELD: ClockIcon,
  CONFIRMED: CheckIcon,
  EXPIRED: ClockIcon,
  CANCELLED: XIcon,
};

/** Circular countdown: the ring empties as the hold runs out of time. */
function CountdownRing({ left, total }: { left: number; total: number }) {
  const radius = 54;
  const circumference = 2 * Math.PI * radius;
  const fraction = total > 0 ? Math.max(0, Math.min(1, left / total)) : 0;
  const urgent = left <= 60_000;
  return (
    <div className={`ring ${urgent ? 'ring-urgent' : ''}`}>
      <svg viewBox="0 0 120 120" aria-hidden="true">
        <circle className="ring-track" cx="60" cy="60" r={radius} />
        <circle
          className="ring-progress"
          cx="60"
          cy="60"
          r={radius}
          strokeDasharray={circumference}
          strokeDashoffset={circumference * (1 - fraction)}
        />
      </svg>
      <div className="ring-center">
        <span className="ring-time countdown" data-testid="countdown">
          {formatRemaining(left)}
        </span>
        <span className="ring-label">বাকি সময়</span>
      </div>
    </div>
  );
}

export function HoldPage() {
  const { holdId = '' } = useParams();
  const { studentId } = useStudent();
  const now = useServerNow();
  const [busy, setBusy] = useState<'pay' | 'cancel' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  // Latest result of an action, shown until the next refresh arrives.
  const [override, setOverride] = useState<Hold | null>(null);

  // The hold ID is in the URL, so a page refresh reloads the same application (rule 11).
  const load = useCallback(() => api.getHold(holdId), [holdId]);
  const { data, error, loading, reload } = usePolling(load, REFRESH_MS);
  const hold = override ?? data;

  useEffect(() => setOverride(null), [data]);

  const status = hold ? effectiveStatus(hold, now) : null;
  const left = hold ? remainingMs(hold, now) : 0;
  const totalMs = hold?.createdAt ? Date.parse(hold.expiresAt) - Date.parse(hold.createdAt) : left;

  // Rule 9: the moment the countdown reaches zero the page shows EXPIRED and disables payment;
  // then ask the server once to confirm.
  const expiredLocally = hold?.status === 'HELD' && status === 'EXPIRED';
  const askedAfterExpiry = useRef(false);
  useEffect(() => {
    if (expiredLocally && !askedAfterExpiry.current) {
      askedAfterExpiry.current = true;
      void reload();
    }
    if (!expiredLocally) askedAfterExpiry.current = false;
  }, [expiredLocally, reload]);

  async function pay() {
    if (!hold || !studentId || busy) return;
    setBusy('pay');
    setActionError(null);
    try {
      const result = await api.confirm(hold.holdId, studentId);
      setOverride({ ...hold, status: result.status });
      setNotice('ভর্তি ফি জমা হয়েছে। ভর্তি নিশ্চিত হয়েছে!');
    } catch (e) {
      setActionError(toUserMessage(e));
    } finally {
      setBusy(null);
      void reload();
    }
  }

  async function cancel() {
    if (!hold || !studentId || busy) return;
    if (!window.confirm('আপনি কি নিশ্চিত যে আবেদনটি বাতিল করতে চান? আসনটি অন্যদের জন্য খুলে যাবে।')) return;
    setBusy('cancel');
    setActionError(null);
    try {
      await api.cancel(hold.holdId, studentId);
      setOverride({ ...hold, status: 'CANCELLED' });
      setNotice('আবেদন বাতিল করা হয়েছে।');
    } catch (e) {
      setActionError(toUserMessage(e));
    } finally {
      setBusy(null);
      void reload();
    }
  }

  if (!hold || !status) {
    const notFound = error instanceof ApiError && error.status === 404;
    return (
      <section className="page">
        {loading && <Spinner />}
        {error != null && <Alert kind="error">{notFound ? 'এই আবেদনটি পাওয়া যায়নি।' : toUserMessage(error)}</Alert>}
        <div className="link-row">
          <Link to="/me">আমার আবেদন</Link>
          <Link to="/">শ্রেণি তালিকা</Link>
        </div>
      </section>
    );
  }

  const canAct = status === 'HELD' && !!studentId && busy === null;
  const copy = STATUS_COPY[status];
  const StatusIcon = STATUS_ICON[status];

  return (
    <section className="page">
      {notice && <Alert kind="success">{notice}</Alert>}
      {actionError && <Alert kind="error">{actionError}</Alert>}
      {error != null && <Alert kind="error">{toUserMessage(error)}</Alert>}
      {!studentId && status === 'HELD' && (
        <Alert kind="info">ফি দিতে বা বাতিল করতে উপরে শিক্ষার্থীর জন্ম নিবন্ধন নম্বর দিন।</Alert>
      )}

      <div className={`hold-card status-${status.toLowerCase()}`}>
        <div className="hold-banner">
          <span className="hold-banner-icon">
            <StatusIcon />
          </span>
          <div>
            <h1>{copy.title}</h1>
            <p>{copy.text}</p>
          </div>
        </div>

        <div className="hold-body">
          <div className="hold-visual">
            {status === 'HELD' ? (
              <CountdownRing left={left} total={totalMs} />
            ) : (
              <div className="status-emblem">
                <StatusIcon />
              </div>
            )}
          </div>

          <dl className="details">
            <div>
              <dt>শ্রেণি</dt>
              <dd className="detail-strong">{hold.className ?? hold.classId}</dd>
            </div>
            <div>
              <dt>অবস্থা</dt>
              <dd data-testid="hold-status">
                <StatusBadge status={status} />
              </dd>
            </div>
            <div>
              <dt>মেয়াদ শেষের সময়</dt>
              <dd>{formatDateTime(hold.expiresAt)}</dd>
            </div>
            {studentId && (
              <div>
                <dt>শিক্ষার্থী</dt>
                <dd className="mono">{studentId}</dd>
              </div>
            )}
          </dl>
        </div>

        <div className="hold-actions">
          <button type="button" className="button success lg" onClick={() => void pay()} disabled={!canAct}>
            <WalletIcon />
            {busy === 'pay' ? 'জমা হচ্ছে…' : 'ভর্তি ফি দিন'}
          </button>
          <button type="button" className="button danger-outline lg" onClick={() => void cancel()} disabled={!canAct}>
            {busy === 'cancel' ? 'বাতিল হচ্ছে…' : 'বাতিল'}
          </button>
        </div>

        {status === 'EXPIRED' && (
          <div className="hold-footer-note">
            চাইলে <Link to="/">শ্রেণি তালিকা</Link> থেকে আবার আবেদন করতে পারেন।
          </div>
        )}
      </div>

      <div className="link-row">
        <Link to="/me">
          <FileIcon /> আমার সব আবেদন
        </Link>
        <Link to="/">শ্রেণি তালিকা</Link>
      </div>
    </section>
  );
}
