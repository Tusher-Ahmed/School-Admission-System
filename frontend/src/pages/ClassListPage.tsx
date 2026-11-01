import { useCallback, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, toUserMessage } from '../api/client';
import { Alert } from '../components/common';
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
  const activeByClass = new Map(
    (mine.data ?? [])
      .filter((h) => ['HELD', 'CONFIRMED'].includes(effectiveStatus(h, now)))
      .map((h) => [h.classId, h]),
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

  return (
    <section className="page">
      <h1>শ্রেণি তালিকা</h1>

      {!studentId && <Alert kind="info">আবেদন করতে আগে উপরে শিক্ষার্থীর জন্ম নিবন্ধন নম্বর দিন।</Alert>}
      {actionError && <Alert kind="error">{actionError}</Alert>}
      {classes.error != null && <Alert kind="error">{toUserMessage(classes.error)}</Alert>}

      {classes.loading && !classes.data && <p className="muted">লোড হচ্ছে…</p>}

      {classes.data && classes.data.length === 0 && (
        <Alert kind="info">
          এখনও কোনো শ্রেণি তৈরি হয়নি। <Link to="/classes/new">নতুন শ্রেণি তৈরি করুন</Link>।
        </Alert>
      )}

      {classes.data && classes.data.length > 0 && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>শ্রেণি</th>
                <th className="num">মোট আসন</th>
                <th className="num">hold-এ আছে</th>
                <th className="num">নিশ্চিত</th>
                <th className="num">খালি</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {classes.data.map((c) => {
                const active = activeByClass.get(c.id);
                return (
                  <tr key={c.id}>
                    <td>{c.name}</td>
                    <td className="num">{c.totalSeats}</td>
                    <td className="num">{c.heldSeats}</td>
                    <td className="num">{c.confirmedSeats}</td>
                    <td className={`num ${c.availableSeats === 0 ? 'full' : ''}`}>{c.availableSeats}</td>
                    <td className="actions">
                      {active ? (
                        <Link to={`/holds/${active.holdId}`}>আবেদন দেখুন</Link>
                      ) : (
                        <button
                          type="button"
                          onClick={() => void apply(c.id)}
                          disabled={!studentId || applyingTo !== null || c.availableSeats === 0}
                          title={c.availableSeats === 0 ? 'কোনো আসন খালি নেই' : undefined}
                        >
                          {applyingTo === c.id ? 'আবেদন হচ্ছে…' : c.availableSeats === 0 ? 'আসন নেই' : 'আবেদন করুন'}
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      <p className="muted small">আসনের হিসাব প্রতি {REFRESH_MS / 1000} সেকেন্ডে নিজে থেকে হালনাগাদ হয়।</p>
    </section>
  );
}
