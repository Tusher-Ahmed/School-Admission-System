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

### Backend (xUnit, 101 tests)

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

### Frontend (Vitest + Testing Library, 35 tests)

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
| 4 | `POST /classes/{classId}/holds` | 201 | 400 bad `X-Student-Id`, 404 class, 409 `CLASS_FULL` / `ALREADY_APPLIED` / `ALREADY_APPLIED_THIS_YEAR` (one active application per student per year, see ASSUMPTIONS) |
| 5 | `GET /holds/{holdId}` | 200 | 404 |
| 6 | `POST /holds/{holdId}/confirm` | 200 (also when repeated) | 400, 403 not your hold, 404, 409 expired / cancelled |
| 7 | `DELETE /holds/{holdId}` | 204 (also when repeated) | 400, 403, 404, 409 already confirmed / expired |
| 8 | `GET /me/holds` | 200 | 400 |

Every error has the body `{ "error": "CODE", "message": "human readable text" }`. Bad input never causes a 500: invalid values → `400`, request body over 16 KB → `413`. If the database is unreachable the API answers `503 DATABASE_UNAVAILABLE`; if an application waits more than 5 s for the class lock during a rush → `503 SERVER_BUSY` (retry).

Responses contain every field of the brief's contract; a few extra fields are added (`className`, `createdAt`, `confirmedAt`). IDs are UUID strings.

## 7. How it works – implemented logic

### 7.1 Data model ([`schema.sql`](backend/src/SchoolAdmission.Api/Data/schema.sql))

1. **`classes`** – one row per class *and shift* (`Class 6 - Morning Shift` and `Class 6 - Day Shift` are two classes): `id`, `name`, `total_seats`, `created_at`.
2. **`holds`** – one row per application: `id`, `class_id`, `student_id` (17-digit text), `status` (`HELD` / `CONFIRMED` / `EXPIRED` / `CANCELLED`), `created_at`, `expires_at`, `confirmed_at`, `cancelled_at`.
3. **Seat numbers are never stored.** They are counted from `holds` on every request, so they can never drift and a repeated payment can never count twice.
4. **Status changes are `UPDATE`s of the same row** (pay, cancel, expire). A new row only appears when a new application is made, so the history stays visible.
5. **Database guards:** `CHECK` constraints (seats > 0, 17-digit student ID, allowed statuses), a foreign key from `holds` to `classes`, and the partial unique index `ux_holds_active (class_id, student_id) WHERE status IN ('HELD','CONFIRMED')` – PostgreSQL itself refuses a second active application of a student in one class.
6. The backend runs `schema.sql` at every startup (`IF NOT EXISTS`, under an advisory lock so two processes starting together do not race). No migrations, no manual table creation needed.

### 7.2 Student identity

1. No login. The student is the 17-digit birth registration number sent in the `X-Student-Id` header.
2. One shared filter checks the header **before any database work**: missing → `400 MISSING_STUDENT_ID`, not exactly 17 ASCII digits (after trimming) → `400 INVALID_STUDENT_ID`.
3. The UI converts Bangla digits (০–৯) and removes spaces/dashes before sending, so the same child always has the same ID.

### 7.3 Create a class – `POST /classes`

1. `name` is trimmed; it must be non-empty, at most 200 characters and contain no control characters (NUL, line breaks).
2. `totalSeats` must be a whole number from 1 to 100 000. Strings, decimals and malformed JSON → `400`.
3. The class is inserted and returned as `201 { id, name, totalSeats }`.

### 7.4 Seat counts – `GET /classes`, `GET /classes/{classId}`

For every class, in one query:

- `heldSeats` = holds with `status = 'HELD' AND expires_at > now()`
- `confirmedSeats` = holds with `status = 'CONFIRMED'`
- `availableSeats` = `totalSeats − heldSeats − confirmedSeats`

`now()` is the **database clock**, so all backend processes agree on time. A hold whose time is up stops counting at that exact moment, even before anything changes its row.

### 7.5 Apply for a seat – `POST /classes/{classId}/holds`

One database transaction, in this order:

1. **Validate** `X-Student-Id` (7.2).
2. **Lock the student** – `pg_advisory_xact_lock(hash(student_id))`. Two simultaneous applications by the same student are handled one after the other.
3. **Lock the class row** – `SELECT … FROM classes WHERE id = @id FOR UPDATE`. Every other application for this class waits here. Class not found → `404`.
4. **Expire stale holds** of this class (`HELD` with `expires_at <= now()` → `EXPIRED`).
5. **Same class check (rule 3)** – the student already has a `HELD`/`CONFIRMED` hold in this class → `409 ALREADY_APPLIED`.
6. **Once-a-year check** – the student already has an application **made this year** (any class) that is not expired or cancelled → `409 ALREADY_APPLIED_THIS_YEAR` (with `existingHoldId`, `existingClassName`). Cancelled, expired and last year's applications do not block.
7. **Capacity check (rule 1)** – `held + confirmed >= totalSeats` → `409 CLASS_FULL`.
8. **Insert** the hold: `status = 'HELD'`, `expires_at = now() + HOLD_DURATION_SECONDS`. Commit → `201 { holdId, classId, studentId, status, expiresAt }`.
9. If the unique index ever fires (safety net) → `409 ALREADY_APPLIED`, never `500`.
10. The class lock waits at most 5 s (`lock_timeout`); during an extreme rush the request returns `503 SERVER_BUSY` instead of hanging.

Locks are always taken in the same order (student, then class), so there are no deadlocks. Both are PostgreSQL locks, so this works the same with several backend processes.

### 7.6 Read applications – `GET /holds/{holdId}`, `GET /me/holds`

1. `GET /holds/{holdId}` returns `holdId`, `classId`, `status`, `expiresAt` (+ `className`, `createdAt`). It needs no header and therefore does not return `studentId`; hold IDs are random UUIDs.
2. `GET /me/holds` returns only the caller's applications, newest first.
3. Both report the **effective status**: a `HELD` hold past `expires_at` is shown as `EXPIRED` immediately.

### 7.7 Pay the fee – `POST /holds/{holdId}/confirm`

1. One conditional statement does the check and the change together:
   `UPDATE holds SET status = 'CONFIRMED', confirmed_at = now() WHERE id = @id AND student_id = @sid AND status = 'HELD' AND expires_at > now()`.
2. 1 row changed → `200 { holdId, status: "CONFIRMED" }`.
3. 0 rows → find out why: unknown hold → `404`; someone else's → `403 NOT_YOUR_APPLICATION`; **already confirmed by this student → `200` again** (idempotent, `confirmedSeats` grows only once – rule 5); cancelled → `409 HOLD_CANCELLED`; expired → `409 HOLD_EXPIRED` (rule 4).

### 7.8 Cancel – `DELETE /holds/{holdId}`

1. Same pattern: `UPDATE … SET status = 'CANCELLED' WHERE id = @id AND student_id = @sid AND status = 'HELD' AND expires_at > now()`.
2. Changed → `204`, the seat is free immediately. Already cancelled → `204` again.
3. Someone else's → `403`; confirmed → `409 ALREADY_CONFIRMED`; expired → `409 HOLD_EXPIRED`.
4. Because confirm and cancel are both conditional on `status = 'HELD'`, a payment and a cancellation arriving at the same moment cannot both succeed.

### 7.9 Expiry (rule 2)

1. **The seat comes back at the exact expiry moment** without any API call, because every count and status check compares `expires_at` with `now()`.
2. A background job (`HoldExpirySweeper`) runs every 5 s and stores `status = 'EXPIRED'` on timed-out rows, so the table itself is also correct. It survives database errors (logs and retries).
3. Duration from `HOLD_DURATION_SECONDS` (default 600); an invalid value stops the app at startup with a clear message.

### 7.10 Errors and robustness (rule 7)

1. Every error body is `{ "error": "CODE", "message": "…" }`.
2. Bad input never produces a `500`: validation → `400`, unknown or non-UUID ids → `404`, wrong owner → `403`, conflicts → `409`, body over 16 KB → `413`.
3. Database errors are classified: cannot reach the database → `503 DATABASE_UNAVAILABLE`; data the database rejects → `4xx`; anything else is logged as a bug (`500`).
4. Data lives only in PostgreSQL, so a restart loses nothing (rule 6).

### 7.11 Security

1. CORS allows only the local frontend (`localhost` / `127.0.0.1`, any port) and origins listed in `CORS_ORIGINS`.
2. All SQL is parameterised (no string concatenation). Security headers: `nosniff`, `X-Frame-Options: DENY`, `no-referrer`, `Cache-Control: no-store`.
3. The connection string comes only from environment variables or the gitignored `backend/.env`; it is never logged or committed.

### 7.12 Frontend logic

1. **Student box** at the top of every student page: validates 17 digits, converts Bangla digits, saves the ID in `localStorage` – applications are still there after refresh or closing the browser (rule 11). Hidden on the admin page.
2. **Switching student** resets every page, and the application page only shows an application that belongs to the current student – nothing of the previous student stays on screen.
3. **Class list** refreshes every 3 s (rule 10): cards with a seat bar, the four numbers, *full* badge. "আবেদন করুন" is disabled when the class is full or the student already has an active application this year (with a notice and a link to it).
4. **Application page**: countdown computed from the server's `expiresAt` (corrected for a wrong computer clock using the response `Date` header). At zero it shows `EXPIRED` and disables payment **without reloading** (rule 9), then re-checks with the server. The URL contains the hold ID, so refresh reloads it.
5. **My applications**: the student's applications with live status and remaining time.
6. **Errors** (seat gone, expired, invalid input, server down) are shown as Bangla messages; network failures never leave a blank page; an error boundary catches unexpected UI errors (rule 12).
7. Buttons are disabled while their request is running, so a double click sends one request.

### 7.13 Brief rules → where they are implemented and tested

| Rule | Implementation | Tests |
|---|---|---|
| 1 Never more holds than seats | class row lock + capacity check (7.5) | `ConcurrencyTests`, `MultiInstanceTests`, `two-process-check.ps1` |
| 2 Automatic expiry | `expires_at` vs `now()` + sweeper (7.9) | `ExpiryTests`, Playwright step 9 |
| 3 One active per class | same-class check + `ux_holds_active` (7.5) | `Apply_SameStudentTwice…`, `SameStudentAppliesManyTimesAtOnce…` |
| 4 Confirm only own unexpired HELD | conditional `UPDATE` (7.7) | `Confirm_OtherStudentsHold…`, `Confirm_ExpiredHold…` |
| 5 Confirm counts once | idempotent confirm, counts not stored (7.7) | `Confirm_Twice_CountsOnce`, `ConfirmManyTimesAtOnce…` |
| 6 No data loss on restart | PostgreSQL only | – |
| 7 Bad input → 4xx | validation + error handler (7.10) | `ClassesApiTests`, `SecurityTests` |
| 8 Everything from the UI | 4 screens (7.12) | Playwright flow |
| 9 EXPIRED without reload | client countdown (7.12) | frontend test + Playwright step 9 |
| 10 Seat counts auto-refresh | 3 s polling | Playwright steps 8, 10 |
| 11 Survives refresh / browser restart | `localStorage` + `GET /me/holds` | frontend test + Playwright step 5 |
| 12 Readable errors | Bangla messages, error boundary | `client.test.ts`, `App.test.tsx` |
| Bonus: two processes | PostgreSQL locks, DB clock | `MultiInstanceTests`, `two-process-check.ps1` |
| Bonus: frontend tests | Vitest + Playwright | 35 unit/component tests + 1 E2E flow |

## 8. More

- [ASSUMPTIONS.md](ASSUMPTIONS.md) – decisions where the brief was open, and how the bonus works
- [AI_USAGE.md](AI_USAGE.md) – how AI tools were used
