import { Link, NavLink, Route, Routes } from 'react-router-dom';
import { StudentBar } from './components/StudentBar';
import { ClassListPage } from './pages/ClassListPage';
import { CreateClassPage } from './pages/CreateClassPage';
import { HoldPage } from './pages/HoldPage';
import { MyHoldsPage } from './pages/MyHoldsPage';

export default function App() {
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
        <StudentBar />
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
