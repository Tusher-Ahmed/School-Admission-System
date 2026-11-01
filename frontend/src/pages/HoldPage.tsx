import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError, api, toUserMessage } from '../api/client';
import type { Hold } from '../api/types';
import { Alert, StatusBadge } from '../components/common';
import { effectiveStatus, formatDateTime, formatRemaining, remainingMs } from '../holds/holdTime';
import { usePolling } from '../hooks/usePolling';
import { useServerNow } from '../hooks/useServerNow';
import { useStudent } from '../student/StudentContext';

const REFRESH_MS = 5000;

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

  if (!hold) {
    const notFound = error instanceof ApiError && error.status === 404;
    return (
      <section className="page">
        <h1>আবেদন</h1>
        {loading && <p className="muted">লোড হচ্ছে…</p>}
        {error != null && <Alert kind="error">{notFound ? 'এই আবেদনটি পাওয়া যায়নি।' : toUserMessage(error)}</Alert>}
        <p>
          <Link to="/me">আমার আবেদন</Link> · <Link to="/">শ্রেণি তালিকা</Link>
        </p>
      </section>
    );
  }

  const canAct = status === 'HELD' && !!studentId && busy === null;

  return (
    <section className="page">
      <h1>আবেদন</h1>

      {notice && <Alert kind="success">{notice}</Alert>}
      {actionError && <Alert kind="error">{actionError}</Alert>}
      {error != null && <Alert kind="error">{toUserMessage(error)}</Alert>}
      {!studentId && status === 'HELD' && (
        <Alert kind="info">ফি দিতে বা বাতিল করতে উপরে শিক্ষার্থীর জন্ম নিবন্ধন নম্বর দিন।</Alert>
      )}

      <div className="card">
        <dl className="details">
          <dt>শ্রেণি</dt>
          <dd>{hold.className ?? hold.classId}</dd>

          <dt>অবস্থা</dt>
          <dd data-testid="hold-status">{status && <StatusBadge status={status} />}</dd>

          {status === 'HELD' && (
            <>
              <dt>মেয়াদ শেষ হতে বাকি</dt>
              <dd className="countdown" data-testid="countdown">
                {formatRemaining(left)}
              </dd>
            </>
          )}

          <dt>মেয়াদ শেষের সময়</dt>
          <dd>{formatDateTime(hold.expiresAt)}</dd>
        </dl>

        {status === 'EXPIRED' && (
          <Alert kind="info">
            মেয়াদ শেষ হয়ে গেছে, আসনটি অন্যদের জন্য খুলে দেওয়া হয়েছে। চাইলে <Link to="/">আবার আবেদন করুন</Link>।
          </Alert>
        )}

        <div className="button-row">
          <button type="button" onClick={() => void pay()} disabled={!canAct}>
            {busy === 'pay' ? 'জমা হচ্ছে…' : 'ভর্তি ফি দিন'}
          </button>
          <button type="button" className="danger" onClick={() => void cancel()} disabled={!canAct}>
            {busy === 'cancel' ? 'বাতিল হচ্ছে…' : 'বাতিল'}
          </button>
        </div>
      </div>

      <p>
        <Link to="/me">আমার সব আবেদন</Link> · <Link to="/">শ্রেণি তালিকা</Link>
      </p>
    </section>
  );
}
