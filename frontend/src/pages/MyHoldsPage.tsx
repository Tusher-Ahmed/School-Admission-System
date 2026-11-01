import { useCallback } from 'react';
import { Link } from 'react-router-dom';
import { api, toUserMessage } from '../api/client';
import { Alert, StatusBadge } from '../components/common';
import { effectiveStatus, formatDateTime, formatRemaining, remainingMs } from '../holds/holdTime';
import { usePolling } from '../hooks/usePolling';
import { useServerNow } from '../hooks/useServerNow';
import { useStudent } from '../student/StudentContext';

const REFRESH_MS = 5000;

export function MyHoldsPage() {
  const { studentId } = useStudent();
  const now = useServerNow();

  // Loaded from the server with the saved ID, so applications are back after refresh or browser restart (rule 11).
  const load = useCallback(() => api.myHolds(studentId!), [studentId]);
  const { data, error, loading } = usePolling(studentId ? load : null, REFRESH_MS);

  return (
    <section className="page">
      <h1>আমার আবেদন</h1>

      {!studentId && <Alert kind="info">আপনার আবেদনগুলো দেখতে উপরে শিক্ষার্থীর জন্ম নিবন্ধন নম্বর দিন।</Alert>}
      {error != null && <Alert kind="error">{toUserMessage(error)}</Alert>}
      {studentId && loading && !data && <p className="muted">লোড হচ্ছে…</p>}

      {data && data.length === 0 && (
        <Alert kind="info">
          এই শিক্ষার্থীর কোনো আবেদন নেই। <Link to="/">শ্রেণি তালিকা</Link> থেকে আবেদন করুন।
        </Alert>
      )}

      {data && data.length > 0 && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>শ্রেণি</th>
                <th>অবস্থা</th>
                <th>বাকি সময়</th>
                <th>মেয়াদ শেষের সময়</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {data.map((h) => {
                const status = effectiveStatus(h, now);
                return (
                  <tr key={h.holdId}>
                    <td>{h.className ?? h.classId}</td>
                    <td>
                      <StatusBadge status={status} />
                    </td>
                    <td className="countdown">{status === 'HELD' ? formatRemaining(remainingMs(h, now)) : '—'}</td>
                    <td>{formatDateTime(h.expiresAt)}</td>
                    <td className="actions">
                      <Link to={`/holds/${h.holdId}`}>{status === 'HELD' ? 'ফি দিন / বাতিল' : 'বিস্তারিত'}</Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
