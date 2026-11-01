import { useCallback, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, toUserMessage } from '../api/client';
import { Alert, PageHeader, SeatBar, Spinner } from '../components/common';
import { ArrowIcon, ListIcon, SeatIcon } from '../components/icons';
import { effectiveStatus } from '../holds/holdTime';
import { usePolling } from '../hooks/usePolling';
import { useServerNow } from '../hooks/useServerNow';
import { useStudent } from '../student/StudentContext';

const REFRESH_MS = 3000;

export function ClassListPage() {
  const { studentId } = useStudent();
  const navigate = useNavigate();
  const now = useServerNow();
  const [applyingTo, setApplyingTo] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  // Rule 10: seat numbers refresh every few seconds without reloading the page.
  const classes = usePolling(api.listClasses, REFRESH_MS);
  const loadMine = useCallback(() => api.myHolds(studentId!), [studentId]);
  const mine = usePolling(studentId ? loadMine : null, REFRESH_MS);

  // The student's active application per class, to link to it instead of offering "apply" again.
  const activeHolds = (mine.data ?? []).filter((h) => ['HELD', 'CONFIRMED'].includes(effectiveStatus(h, now)));
  const activeByClass = new Map(activeHolds.map((h) => [h.classId, h]));

  // One active application per year: while one exists, other classes cannot be applied to.
  const thisYear = new Date(now).getFullYear();
  const activeThisYear = activeHolds.find(
    (h) => !h.createdAt || new Date(h.createdAt).getFullYear() === thisYear,
  );

  async function apply(classId: string) {
    if (!studentId || applyingTo) return;
    setApplyingTo(classId);
    setActionError(null);
    try {
      const hold = await api.apply(classId, studentId);
      navigate(`/holds/${hold.holdId}`);
    } catch (e) {
      setActionError(toUserMessage(e));
      void classes.reload();
      void mine.reload();
    } finally {
      setApplyingTo(null);
    }
  }

  const list = classes.data ?? [];
  const totals = list.reduce(
    (t, c) => ({ seats: t.seats + c.totalSeats, available: t.available + c.availableSeats }),
    { seats: 0, available: 0 },
  );

  return (
    <section className="page">
      <PageHeader
        icon={<ListIcon />}
        title="শ্রেণি তালিকা"
        subtitle={
          <>
            <span className="live-dot" aria-hidden="true" /> আসনের হিসাব প্রতি {REFRESH_MS / 1000} সেকেন্ডে নিজে থেকে
            হালনাগাদ হয়
          </>
        }
      />

      {!studentId && <Alert kind="info">আবেদন করতে আগে উপরে শিক্ষার্থীর জন্ম নিবন্ধন নম্বর দিন।</Alert>}
      {activeThisYear && (
        <Alert kind="info">
          {effectiveStatus(activeThisYear, now) === 'CONFIRMED'
            ? `এই বছরে "${activeThisYear.className}" শ্রেণিতে ভর্তি নিশ্চিত হয়েছে, তাই অন্য শ্রেণিতে আবেদন করা যাবে না।`
            : `এই বছরে "${activeThisYear.className}" শ্রেণিতে একটি চলমান আবেদন আছে। অন্য শ্রেণিতে আবেদন করতে আগে সেটি বাতিল করুন বা মেয়াদ শেষ হতে দিন।`}{' '}
          <Link to={`/holds/${activeThisYear.holdId}`}>আবেদনটি দেখুন</Link>
        </Alert>
      )}
      {actionError && <Alert kind="error">{actionError}</Alert>}
      {classes.error != null && <Alert kind="error">{toUserMessage(classes.error)}</Alert>}

      {classes.loading && !classes.data && <Spinner />}

      {list.length > 0 && (
        <div className="stats">
          <div className="stat">
            <span className="stat-value">{list.length}</span>
            <span className="stat-label">শ্রেণি / shift</span>
          </div>
          <div className="stat">
            <span className="stat-value">{totals.seats}</span>
            <span className="stat-label">মোট আসন</span>
          </div>
          <div className="stat stat-accent">
            <span className="stat-value">{totals.available}</span>
            <span className="stat-label">এখন খালি</span>
          </div>
        </div>
      )}

      {classes.data && list.length === 0 && (
        <div className="empty-state card">
          <SeatIcon className="empty-icon" />
          <p>এখনও কোনো শ্রেণি তৈরি হয়নি।</p>
          <Link to="/classes/new" className="button">
            নতুন শ্রেণি তৈরি করুন
          </Link>
        </div>
      )}

      <div className="class-grid">
        {list.map((c) => {
          const active = activeByClass.get(c.id);
          const full = c.availableSeats === 0;
          return (
            <article key={c.id} className={`class-card ${full ? 'is-full' : ''}`} data-testid="class-card">
              <div className="class-card-head">
                <h2 className="class-name" data-testid="class-name">
                  {c.name}
                </h2>
                {full ? (
                  <span className="pill pill-danger">পূর্ণ</span>
                ) : (
                  <span className="pill pill-success">{c.availableSeats} খালি</span>
                )}
              </div>

              <SeatBar total={c.totalSeats} held={c.heldSeats} confirmed={c.confirmedSeats} />

              <dl className="seat-stats">
                <div>
                  <dt>মোট আসন</dt>
                  <dd data-testid="seats-total">{c.totalSeats}</dd>
                </div>
                <div>
                  <dt>
                    <span className="legend legend-held" /> hold-এ আছে
                  </dt>
                  <dd data-testid="seats-held">{c.heldSeats}</dd>
                </div>
                <div>
                  <dt>
                    <span className="legend legend-confirmed" /> নিশ্চিত
                  </dt>
                  <dd data-testid="seats-confirmed">{c.confirmedSeats}</dd>
                </div>
                <div>
                  <dt>খালি</dt>
                  <dd data-testid="seats-available" className={full ? 'text-danger' : 'text-success'}>
                    {c.availableSeats}
                  </dd>
                </div>
              </dl>

              <div className="class-card-foot">
                {active ? (
                  <Link to={`/holds/${active.holdId}`} className="button secondary block">
                    আবেদন দেখুন <ArrowIcon />
                  </Link>
                ) : activeThisYear ? (
                  <button type="button" className="button block" disabled title="এই বছরে অন্য শ্রেণিতে আবেদন আছে">
                    অন্য শ্রেণিতে আবেদন আছে
                  </button>
                ) : (
                  <button
                    type="button"
                    className="button block"
                    onClick={() => void apply(c.id)}
                    disabled={!studentId || applyingTo !== null || full}
                    title={full ? 'কোনো আসন খালি নেই' : undefined}
                  >
                    {applyingTo === c.id ? 'আবেদন হচ্ছে…' : full ? 'আসন নেই' : 'আবেদন করুন'}
                  </button>
                )}
              </div>
            </article>
          );
        })}
      </div>
    </section>
  );
}
