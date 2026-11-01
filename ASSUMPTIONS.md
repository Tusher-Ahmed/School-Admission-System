# Assumptions

Decisions where the brief left room, and why.

## Identity

- **The student is identified only by the 17-digit birth registration number** in `X-Student-Id`. There is no login (as the brief says), so anyone can type any number; real authentication is out of scope.
- The ID is **trimmed** and must be **exactly 17 ASCII digits**, otherwise `400`. It is stored as `char(17)` text so leading zeros are kept.
- The **UI converts Bangla digits** (০–৯) and removes spaces/dashes before sending, so the same child always gets the same ID. The API itself only accepts ASCII digits, as in the contract.
- The UI keeps the ID in `localStorage`, so applications are still visible after refresh or closing the browser. If storage is blocked the app still works; the ID just has to be typed again. Typing the same ID on another device shows the same applications.

## Holds

- **One active application per student per class** = at most one `HELD` or `CONFIRMED` hold. After `CANCELLED` or `EXPIRED` the student may apply to that class again. A student may hold seats in **different** classes at the same time.
- **Expiry uses the database clock** (`now()`) everywhere, so several backend processes can never disagree about time.
- A `HELD` hold is treated as `EXPIRED` **the moment** `expires_at` passes: seat counts, `GET /holds`, confirm and cancel all compare with `now()`. A background job additionally stores `status = 'EXPIRED'` every 5 seconds. So the seat returns on time even if the job is late or the app was down.
- **Confirm is idempotent:** confirming your own already-confirmed hold returns `200` again and does not count twice.
- **Cancel** works only on your own `HELD` hold. Cancelling it again returns `204`. A `CONFIRMED` admission (fee paid) cannot be cancelled through this API (`409 ALREADY_CONFIRMED`); refunds are out of scope. Cancelling an expired hold returns `409 HOLD_EXPIRED` because there is nothing left to release.
- **Someone else's hold:** confirm/cancel → `403` (the hold exists but is not yours). `GET /holds/{holdId}` needs no header and therefore does **not** return `studentId`; hold IDs are random UUIDs, so they cannot be guessed.
- Expired or failed attempts never change seat counts; `availableSeats = totalSeats − heldSeats − confirmedSeats`, all computed from the hold rows (no stored counters that could drift).

## Classes and input

- `name` is required, trimmed, at most 200 characters; names do not have to be unique.
- `totalSeats` must be a whole number from 1 to 100 000. Strings (`"120"`), decimals (`1.5`) and malformed JSON → `400`.
- Class and hold IDs are **UUID strings** (the brief allows any format). A non-UUID ID in a URL → `404`.
- Error bodies are `{ "error": "CODE", "message": "..." }`. Database unreachable → `503 DATABASE_UNAVAILABLE`. Responses contain a few extra fields (`className`, `createdAt`, `confirmedAt`); no contract field is removed or renamed.
- Classes cannot be edited or deleted; the brief only asks for creating them.

## Concurrency (rule 1) and the bonus: two backend processes

Applying for a seat is one database transaction:

1. `SELECT … FROM classes WHERE id = @id FOR UPDATE` – **locks the class row**. Every other application for the same class waits here until this transaction ends.
2. Mark this class's timed-out `HELD` holds as `EXPIRED`.
3. If the student already has an active hold in the class → `409 ALREADY_APPLIED`.
4. If `held + confirmed >= totalSeats` → `409 CLASS_FULL`.
5. Insert the hold with `expires_at = now() + HOLD_DURATION_SECONDS`, commit.

Because the lock is a **PostgreSQL row lock** and not anything in process memory, it works the same for two (or more) backend processes on different ports sharing the database. A partial unique index `ux_holds_active (class_id, student_id) WHERE status IN ('HELD','CONFIRMED')` is a second safety net for rule 3; if it ever fires, the API returns `409`, not `500`.

Confirm and cancel are single conditional `UPDATE … WHERE id = @id AND student_id = @sid AND status = 'HELD' AND expires_at > now()` statements, so the check and the change are atomic; a confirm and a cancel racing each other cannot both succeed.

Verified by `ConcurrencyTests`, `MultiInstanceTests` (two separate API instances) and `backend/scripts/two-process-check.ps1` (two real processes on 8080 and 8081, 60 simultaneous requests for 10 seats → exactly 10 holds).

Schema creation at startup is guarded by a PostgreSQL advisory lock, so two processes starting at the same moment do not race.

## Tests and data

- Backend tests run against the configured PostgreSQL database (no Docker needed). They never delete data; each test creates its own `TEST …` class and random student IDs. `TEST_DB_CONNECTION` can point them at a separate database; `backend/scripts/cleanup-test-data.sql` removes test data.

## Not done / would do with more time

- Real authentication (e.g. OTP to the parent's phone) instead of trusting the typed ID.
- Pushing seat updates to the browser (SignalR / server-sent events) instead of polling every 3 s.
- Admin features: edit/close a class, list applicants, refunds for cancelled confirmed admissions.
- Rate limiting per student/IP for the opening-minute rush.
- A Docker Compose file for one-command setup.
