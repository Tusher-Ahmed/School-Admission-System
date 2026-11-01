import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { isValidStudentId } from './studentId';

const STORAGE_KEY = 'school-admission.studentId';

// localStorage survives page refresh and closing the browser (rule 11). It can be blocked
// (private mode, strict settings); then the app still works, the ID just isn't remembered.
function loadSaved(): string | null {
  try {
    const saved = window.localStorage.getItem(STORAGE_KEY);
    return saved && isValidStudentId(saved) ? saved : null;
  } catch {
    return null;
  }
}

function save(id: string | null) {
  try {
    if (id) window.localStorage.setItem(STORAGE_KEY, id);
    else window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Storage unavailable: keep the ID in memory only.
  }
}

interface StudentContextValue {
  studentId: string | null;
  setStudentId: (id: string | null) => void;
}

const StudentContext = createContext<StudentContextValue | null>(null);

export function StudentProvider({ children }: { children: ReactNode }) {
  const [studentId, setState] = useState<string | null>(loadSaved);

  const setStudentId = useCallback((id: string | null) => {
    save(id);
    setState(id);
  }, []);

  const value = useMemo(() => ({ studentId, setStudentId }), [studentId, setStudentId]);
  return <StudentContext.Provider value={value}>{children}</StudentContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useStudent(): StudentContextValue {
  const ctx = useContext(StudentContext);
  if (!ctx) throw new Error('useStudent must be used inside <StudentProvider>');
  return ctx;
}
