-- Removes the data the automated tests create (classes named "TEST ..." and "E2E ...") and their holds.
-- Real classes are not touched. Run in pgAdmin's Query Tool, or:
--   psql -h localhost -U postgres -d SchoolAdmission -f scripts/cleanup-test-data.sql

BEGIN;

DELETE FROM holds
WHERE class_id IN (SELECT id FROM classes WHERE name LIKE 'TEST %' OR name LIKE 'E2E %');

DELETE FROM classes
WHERE name LIKE 'TEST %' OR name LIKE 'E2E %';

COMMIT;
