import { Link, NavLink, Route, Routes, useLocation } from 'react-router-dom';
import { FileIcon, ListIcon, PlusIcon, SchoolIcon } from './components/icons';
import { StudentBar } from './components/StudentBar';
import { ClassListPage } from './pages/ClassListPage';
import { CreateClassPage } from './pages/CreateClassPage';
import { HoldPage } from './pages/HoldPage';
import { MyHoldsPage } from './pages/MyHoldsPage';
import { useStudent } from './student/StudentContext';

// The admin page has nothing to do with a student, so the student ID box is not shown there.
const ADMIN_PATHS = ['/classes/new'];

export default function App() {
  const { pathname } = useLocation();
  const { studentId } = useStudent();
  const isAdminPage = ADMIN_PATHS.includes(pathname);

  return (
    <div className="app">
      <header className="header">
        <div className="container header-inner">
          <Link to="/" className="brand">
            <span className="brand-logo">
              <SchoolIcon />
            </span>
            <span>
              <span className="brand-title">স্কুল ভর্তি</span>
              <span className="brand-sub">অনলাইন ভর্তি ব্যবস্থা</span>
            </span>
          </Link>
          <nav>
            <NavLink to="/" end>
              <ListIcon />
              শ্রেণি তালিকা
            </NavLink>
            <NavLink to="/me">
              <FileIcon />
              আমার আবেদন
            </NavLink>
            <NavLink to="/classes/new">
              <PlusIcon />
              নতুন শ্রেণি (এডমিন)
            </NavLink>
          </nav>
        </div>
      </header>

      <main className="container">
        {!isAdminPage && <StudentBar />}
        {/* Keyed by student: switching student remounts the page, so no data, message or error
            of the previous student can stay on screen. */}
        <Routes key={studentId ?? 'no-student'}>
          <Route path="/" element={<ClassListPage />} />
          <Route path="/classes/new" element={<CreateClassPage />} />
          <Route path="/holds/:holdId" element={<HoldPage />} />
          <Route path="/me" element={<MyHoldsPage />} />
          <Route
            path="*"
            element={
              <section className="page empty-state">
                <h1>পেজটি পাওয়া যায়নি</h1>
                <Link to="/" className="button">
                  শ্রেণি তালিকায় ফিরে যান
                </Link>
              </section>
            }
          />
        </Routes>
      </main>

      <footer className="footer">
        <div className="container">আগে আবেদন করলে আগে পাবে · আসন সীমিত</div>
      </footer>
    </div>
  );
}
