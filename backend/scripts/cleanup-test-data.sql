-- Removes the data the automated tests create and their holds:
--   backend tests:    classes named "TEST ..." / "E2E ..."
--   Playwright E2E:   classes ending in "[E2E yyyy-mm-dd hh:mm:ss]"
-- Real classes are not touched. Run in pgAdmin's Query Tool, or:
--   psql -h localhost -U postgres -d SchoolAdmission -f scripts/cleanup-test-data.sql

BEGIN;

DELETE FROM holds
WHERE class_id IN (
    SELECT id FROM classes
    WHERE name LIKE 'TEST %' OR name LIKE 'E2E %' OR name LIKE '%[E2E %]'
);

DELETE FROM classes
WHERE name LIKE 'TEST %' OR name LIKE 'E2E %' OR name LIKE '%[E2E %]';

COMMIT;
