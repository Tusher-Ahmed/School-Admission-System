import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import App from '../App';
import { StudentProvider } from '../student/StudentContext';

/** Renders the whole app at `path`, optionally with a student ID already saved (as after a page refresh). */
export function renderApp(path: string, savedStudentId?: string) {
  if (savedStudentId) window.localStorage.setItem('school-admission.studentId', savedStudentId);
  return render(
    <MemoryRouter initialEntries={[path]}>
      <StudentProvider>
        <App />
      </StudentProvider>
    </MemoryRouter>,
  );
}
