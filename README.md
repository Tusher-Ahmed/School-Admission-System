# School Admission System

First-come-first-served online admission. A parent applies for a seat in a class (one shift = one class), the seat is **held** for a limited time, and paying the admission fee **confirms** it. Holds that are not paid in time **expire** and the seat goes back to the pool automatically.

- **Backend:** C# / .NET 8 minimal API + PostgreSQL (EF Core + Npgsql), `http://localhost:8080`
- **Frontend:** React + TypeScript (Vite), `http://localhost:5173`

```
backend/
  src/SchoolAdmission.Api/       API (Program.cs, Features/, Data/schema.sql)
  tests/SchoolAdmission.Tests/   xUnit integration + concurrency tests
  scripts/                       two-process check, test-data cleanup
frontend/                        React app (src/pages, src/api, ...)
```

**Screen recording (2–3 min):** _link will be added here_

---

## 1. Requirements

| Tool | Version |
|---|---|
| .NET SDK | 8.0 or newer (`dotnet --version`) |
| Node.js | 20 or newer (`node --version`) |
| PostgreSQL | 14 or newer, running on `localhost:5432` |

## 2. PostgreSQL

Create an empty database (any name; `SchoolAdmission` is used below):

```bash
psql -h localhost -U postgres -c 'CREATE DATABASE "SchoolAdmission";'
```

(or in pgAdmin: *Databases → Create → Database…*).

**You do not need to create tables.** The backend runs [`schema.sql`](backend/src/SchoolAdmission.Api/Data/schema.sql) at startup (`CREATE TABLE IF NOT EXISTS …`, safe to run every time). You can also run that file yourself in pgAdmin's Query Tool.

## 3. Backend

The connection comes from the environment variable `ConnectionStrings__Default`.

**Windows PowerShell**
```powershell
cd backend
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=SchoolAdmission;Username=postgres;Password=<your-password>"
$env:HOLD_DURATION_SECONDS = "600"   # optional, default 600
dotnet run --project src/SchoolAdmission.Api
```

**bash / macOS / Linux**
```bash
cd backend
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=SchoolAdmission;Username=postgres;Password=<your-password>"
export HOLD_DURATION_SECONDS=600     # optional, default 600
dotnet run --project src/SchoolAdmission.Api
```

Instead of exporting variables you can copy `backend/.env.example` to `backend/.env` (gitignored) and fill it in; real environment variables always win over `.env`.

The API listens on **http://localhost:8080**. Check: `curl http://localhost:8080/health` → `{"status":"ok","database":"ok"}`.
If the connection string is missing the app stops at startup with a clear message.

| Variable | Default | Meaning |
|---|---|---|
| `ConnectionStrings__Default` | — (required) | Npgsql connection string |
| `HOLD_DURATION_SECONDS` | `600` | How long an application holds a seat. Use e.g. `30` to see expiry quickly. |
| `CORS_ORIGINS` | — | Extra browser origins allowed to call the API, comma separated (e.g. `http://192.168.1.10:5173`). The local frontend on `localhost` / `127.0.0.1` (any port) is always allowed. |

Another port: `dotnet run --project src/SchoolAdmission.Api --urls http://localhost:8081`.

## 4. Frontend

```bash
cd frontend
npm install
npm run dev
```

Open **http://localhost:5173**. The backend URL comes from `VITE_API_BASE_URL` (default `http://localhost:8080`). To change it, create `frontend/.env.local`:

```
VITE_API_BASE_URL=http://localhost:8081
```

and restart `npm run dev`.

### Using the app

1. **নতুন শ্রেণি (এডমিন)** – create a class, e.g. `Class 6 - Morning Shift`, 120 seats.
2. Type a 17-digit birth registration number in the box at the top (Bangla digits work too) → **ব্যবহার করুন**. It is remembered across refresh and browser restarts.
3. **শ্রেণি তালিকা** – seat counts refresh every 3 s; press **আবেদন করুন**.
4. On the application page: live countdown, **ভর্তি ফি দিন** (pay → CONFIRMED) or **বাতিল** (cancel). When the countdown reaches 0 the page shows **EXPIRED** and payment is disabled, without reloading.
5. **আমার আবেদন** – all applications of this student with their status.

## 5. Tests

### Backend (xUnit, 95 tests)

The tests host the real API in memory and use the **real PostgreSQL database** from `ConnectionStrings__Default` (same variable or `backend/.env` as above). They never delete data: each test creates its own class named `TEST …` with random student IDs and only checks that data.

```bash
cd backend
dotnet test
```

- To use a separate database instead, set `TEST_DB_CONNECTION` to its connection string.
- To remove the `TEST …` classes afterwards: run [`scripts/cleanup-test-data.sql`](backend/scripts/cleanup-test-data.sql) in pgAdmin or `psql -h localhost -U postgres -d SchoolAdmission -f scripts/cleanup-test-data.sql`.

Highlights:

| Test | Shows |
|---|---|
| `ConcurrencyTests.ManyStudentsAtOnce_NeverMoreHoldsThanSeats` | 50 simultaneous applications for 10 seats → exactly 10 × 201 and 40 × 409 |
| `ConcurrencyTests.SameStudentAppliesManyTimesAtOnce_OnlyOneHold` | 20 parallel applies by one student → one hold |
| `ConcurrencyTests.ConfirmManyTimesAtOnce_AllOk_CountedOnce` | 10 parallel confirms → `confirmedSeats` = 1 |
| `ConcurrencyTests.ConfirmAndCancelAtOnce_ExactlyOneWins` | confirm vs cancel race, 10 rounds |
| `MultiInstanceTests.TwoInstances_SameClass_NeverMoreHoldsThanSeats` | two separate API instances on one database |
| `ExpiryTests.Hold_ExpiresOnItsOwn_AndSweeperStoresExpired` | real 2-second expiry with no API call |

**Two real processes (bonus):** with the connection variable set,

```powershell
cd backend
dotnet build
powershell -ExecutionPolicy Bypass -File scripts/two-process-check.ps1 -Seats 10 -Requests 60
```

starts the backend on ports 8080 and 8081, sends 60 simultaneous applications split across both, and prints `PASS: exactly 10 holds across two processes.`

### Frontend (Vitest + Testing Library, 33 tests)

```bash
cd frontend
npm test
```

### End-to-end (Playwright, real browser + real backend + real database)

One scenario drives the real UI in Chromium against the real API and PostgreSQL, the same way a parent and an admin would: create two classes, invalid input, apply and pay, refresh and reopen the browser, apply and cancel, a full class, a hold expiring on screen, the freed seat taken by someone else, *my applications*, final seat counts.

```bash
cd frontend
npx playwright install chromium   # first time only
npm run test:e2e                  # add --headed to watch it
npx playwright show-report        # steps, screenshots, video, trace
```

Playwright starts its own backend on port **8090** (with `HOLD_DURATION_SECONDS=15`) and frontend on **5180**, so it does not clash with an app already running on 8080/5173. The backend needs the database connection (environment variable or `backend/.env`, see section 3).

The data is **kept** so it can be inspected: classes are named `Class 6 - Morning Shift [E2E <timestamp>]` and the console prints the student IDs used. `scripts/cleanup-test-data.sql` removes them.

## 6. API

Base URL `http://localhost:8080`, JSON everywhere, student identity in the `X-Student-Id` header (exactly 17 digits).

| # | Endpoint | Success | Errors |
|---|---|---|---|
| 1 | `POST /classes` | 201 | 400 invalid name / totalSeats / JSON |
| 2 | `GET /classes` | 200 | |
| 3 | `GET /classes/{classId}` | 200 | 404 |
| 4 | `POST /classes/{classId}/holds` | 201 | 400 bad `X-Student-Id`, 404 class, 409 `CLASS_FULL` / `ALREADY_APPLIED` |
| 5 | `GET /holds/{holdId}` | 200 | 404 |
| 6 | `POST /holds/{holdId}/confirm` | 200 (also when repeated) | 400, 403 not your hold, 404, 409 expired / cancelled |
| 7 | `DELETE /holds/{holdId}` | 204 (also when repeated) | 400, 403, 404, 409 already confirmed / expired |
| 8 | `GET /me/holds` | 200 | 400 |

Every error has the body `{ "error": "CODE", "message": "human readable text" }`. Bad input never causes a 500: invalid values → `400`, request body over 16 KB → `413`. If the database is unreachable the API answers `503 DATABASE_UNAVAILABLE`; if an application waits more than 5 s for the class lock during a rush → `503 SERVER_BUSY` (retry).

Responses contain every field of the brief's contract; a few extra fields are added (`className`, `createdAt`, `confirmedAt`). IDs are UUID strings.

## 7. More

- [ASSUMPTIONS.md](ASSUMPTIONS.md) – decisions where the brief was open, and how the bonus works
- [AI_USAGE.md](AI_USAGE.md) – how AI tools were used
