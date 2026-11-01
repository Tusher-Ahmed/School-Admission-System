# AI usage

## Tool

**Claude Code** (Anthropic, model Claude Opus 5.5) in VS Code, used throughout the assignment.

## What it was used for

- **Understanding the brief and planning:** turning the Bangla brief into a step-by-step plan, mapping every rule (1–12) to a concrete design, choosing status codes for cases the brief leaves open.
- **Database:** explaining PostgreSQL concepts (transactions, row locks, partial unique indexes) since this was my first PostgreSQL project, and writing `schema.sql`.
- **Backend:** endpoints, the locked apply transaction, confirm/cancel, expiry sweeper, error handling.
- **Tests:** integration tests against the real database, concurrency tests, the two-process script, frontend tests.
- **Frontend:** React pages, API client, countdown, error messages.
- **Docs:** first drafts of README and ASSUMPTIONS.

I reviewed the design decisions, asked for changes, ran the app and the tests myself.

## Where the AI was wrong, and how it was caught

1. **EF Core query that compiled but failed at runtime.** The first version of `GET /classes/{id}` filtered *after* projecting into a C# record. It compiled, but EF Core cannot translate that to SQL, so the endpoint returned **500**. Caught by the integration test `GetClass_UnknownId_Returns404`; fixed by filtering before the projection. The same mistake was then avoided in `HoldService`.
2. **Over-engineered database setup.** The first plan proposed a separate database login and a separate test database. I asked why they were needed; for this project they were not, so the setup was simplified to one database and the tests were designed to never delete data instead.
3. **Two-process script hung.** The AI first blamed PowerShell's HTTP client. The real cause (found by logging the child processes' output) was the **space in the project path** ("Recruit Project") being passed unquoted to `dotnet run`, so the backends never started.
4. **Broken `package.json`.** Editing it with Windows PowerShell 5.1 added a UTF-8 BOM, and the Vite build failed with `Unexpected token`. Caught by `npm run build`; fixed by rewriting the file without BOM.
5. **Small things caught by the compiler / linters:** a named argument inside an EF expression tree (`CS0853`), an unused variable in a test (ESLint), a PowerShell 7-only flag used on PowerShell 5.1.
6. **Git:** the planning file was pushed in the first commit although I wanted it to stay local; the repository history was recreated without it.

## How I checked the AI's work

- Every feature was committed only after its tests passed (77 backend, 32 frontend).
- Concurrency claims were checked by tests that fire 20–60 requests at once, repeated several times, and by running two real backend processes.
- The tables created by hand in pgAdmin were compared with `schema.sql` (columns, indexes, constraints) before writing code against them.
- Before each commit the staged changes were checked for the database password.
