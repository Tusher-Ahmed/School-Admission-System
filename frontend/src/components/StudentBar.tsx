import { useState, type FormEvent } from 'react';
import { useStudent } from '../student/StudentContext';
import { isValidStudentId, normalizeStudentId } from '../student/studentId';
import { UserIcon } from './icons';

/** The student's birth registration number is their identity (no login). Shown at the top of student pages. */
export function StudentBar() {
  const { studentId, setStudentId } = useStudent();
  const [draft, setDraft] = useState('');
  const [error, setError] = useState<string | null>(null);

  function submit(e: FormEvent) {
    e.preventDefault();
    const id = normalizeStudentId(draft);
    if (!isValidStudentId(id)) {
      setError(`জন্ম নিবন্ধন নম্বর ঠিক ১৭ অঙ্কের হতে হবে (এখন ${id.replace(/\D/g, '').length} অঙ্ক)।`);
      return;
    }
    setError(null);
    setDraft('');
    setStudentId(id);
  }

  if (studentId) {
    return (
      <div className="student-bar student-bar-set">
        <span className="avatar">
          <UserIcon />
        </span>
        <div className="student-info">
          <span className="student-label">শিক্ষার্থীর জন্ম নিবন্ধন নম্বর:</span>
          <strong className="student-id" data-testid="current-student">
            {studentId}
          </strong>
        </div>
        <button type="button" className="button ghost" onClick={() => setStudentId(null)}>
          পরিবর্তন করুন
        </button>
      </div>
    );
  }

  return (
    <form className="student-bar" onSubmit={submit} noValidate>
      <span className="avatar muted-avatar">
        <UserIcon />
      </span>
      <div className="student-form">
        <label htmlFor="student-id">শিক্ষার্থীর জন্ম নিবন্ধন নম্বর (১৭ অঙ্ক)</label>
        <div className="input-row">
          <input
            id="student-id"
            inputMode="numeric"
            autoComplete="off"
            placeholder="যেমন: 20152691234567890"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            aria-invalid={error !== null}
            aria-describedby={error ? 'student-id-error' : 'student-id-hint'}
          />
          <button type="submit" className="button">
            ব্যবহার করুন
          </button>
        </div>
        {error ? (
          <span id="student-id-error" className="field-error" role="alert">
            {error}
          </span>
        ) : (
          <span id="student-id-hint" className="field-hint">
            বাংলা বা ইংরেজি অঙ্কে লিখতে পারেন। নম্বরটি এই ব্রাউজারে মনে রাখা হবে।
          </span>
        )}
      </div>
    </form>
  );
}
