import { Link, NavLink, Route, Routes, useLocation } from 'react-router-dom';
import { StudentBar } from './components/StudentBar';
import { ClassListPage } from './pages/ClassListPage';
import { CreateClassPage } from './pages/CreateClassPage';
import { HoldPage } from './pages/HoldPage';
import { MyHoldsPage } from './pages/MyHoldsPage';

// The admin page has nothing to do with a student, so the student ID box is not shown there.
const ADMIN_PATHS = ['/classes/new'];

export default function App() {
  const { pathname } = useLocation();
  const isAdminPage = ADMIN_PATHS.includes(pathname);

  return (
    <>
      <header className="header">
        <div className="header-inner">
          <Link to="/" className="brand">
            স্কুল ভর্তি
          </Link>
          <nav>
            <NavLink to="/" end>
              শ্রেণি তালিকা
            </NavLink>
            <NavLink to="/me">আমার আবেদন</NavLink>
            <NavLink to="/classes/new">নতুন শ্রেণি (এডমিন)</NavLink>
          </nav>
        </div>
        {!isAdminPage && <StudentBar />}
      </header>

      <main>
        <Routes>
          <Route path="/" element={<ClassListPage />} />
          <Route path="/classes/new" element={<CreateClassPage />} />
          <Route path="/holds/:holdId" element={<HoldPage />} />
          <Route path="/me" element={<MyHoldsPage />} />
          <Route
            path="*"
            element={
              <section className="page">
                <h1>পেজটি পাওয়া যায়নি</h1>
                <Link to="/">শ্রেণি তালিকায় ফিরে যান</Link>
              </section>
            }
          />
        </Routes>
      </main>
    </>
  );
}
