import { useState, type FormEvent } from 'react';
import { useStudent } from '../student/StudentContext';
import { isValidStudentId, normalizeStudentId } from '../student/studentId';

/** The box at the top of every page: the student's birth registration number is their identity. */
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
      <div className="student-bar">
        <span>
          শিক্ষার্থীর জন্ম নিবন্ধন নম্বর: <strong data-testid="current-student">{studentId}</strong>
        </span>
        <button type="button" className="secondary" onClick={() => setStudentId(null)}>
          পরিবর্তন করুন
        </button>
      </div>
    );
  }

  return (
    <form className="student-bar" onSubmit={submit} noValidate>
      <label htmlFor="student-id">শিক্ষার্থীর জন্ম নিবন্ধন নম্বর (১৭ অঙ্ক)</label>
      <input
        id="student-id"
        inputMode="numeric"
        autoComplete="off"
        placeholder="যেমন: 20152691234567890"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        aria-invalid={error !== null}
        aria-describedby={error ? 'student-id-error' : undefined}
      />
      <button type="submit">ব্যবহার করুন</button>
      {error && (
        <span id="student-id-error" className="field-error" role="alert">
          {error}
        </span>
      )}
    </form>
  );
}
