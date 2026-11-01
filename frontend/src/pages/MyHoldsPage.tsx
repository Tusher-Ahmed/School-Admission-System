import { useCallback } from 'react';
import { Link } from 'react-router-dom';
import { api, toUserMessage } from '../api/client';
import { Alert, PageHeader, Spinner, StatusBadge } from '../components/common';
import { ArrowIcon, ClockIcon, FileIcon } from '../components/icons';
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
      <PageHeader icon={<FileIcon />} title="আমার আবেদন" subtitle="এই শিক্ষার্থীর সব আবেদন ও তাদের বর্তমান অবস্থা" />

      {!studentId && <Alert kind="info">আপনার আবেদনগুলো দেখতে উপরে শিক্ষার্থীর জন্ম নিবন্ধন নম্বর দিন।</Alert>}
      {error != null && <Alert kind="error">{toUserMessage(error)}</Alert>}
      {studentId && loading && !data && <Spinner />}

      {data && data.length === 0 && (
        <div className="empty-state card">
          <FileIcon className="empty-icon" />
          <p>এই শিক্ষার্থীর কোনো আবেদন নেই।</p>
          <Link to="/" className="button">
            শ্রেণি তালিকা থেকে আবেদন করুন
          </Link>
        </div>
      )}

      {data && data.length > 0 && (
        <ul className="hold-list">
          {data.map((h) => {
            const status = effectiveStatus(h, now);
            return (
              <li key={h.holdId} className={`hold-item status-${status.toLowerCase()}`} data-testid="hold-item">
                <div className="hold-item-main">
                  <h2 className="hold-item-title">{h.className ?? h.classId}</h2>
                  <div className="hold-item-meta">
                    <StatusBadge status={status} />
                    {status === 'HELD' ? (
                      <span className="countdown-chip">
                        <ClockIcon /> <span className="countdown">{formatRemaining(remainingMs(h, now))}</span> বাকি
                      </span>
                    ) : (
                      <span className="muted small">মেয়াদ: {formatDateTime(h.expiresAt)}</span>
                    )}
                  </div>
                </div>
                <Link to={`/holds/${h.holdId}`} className={`button ${status === 'HELD' ? '' : 'secondary'}`}>
                  {status === 'HELD' ? 'ফি দিন / বাতিল' : 'বিস্তারিত'} <ArrowIcon />
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
