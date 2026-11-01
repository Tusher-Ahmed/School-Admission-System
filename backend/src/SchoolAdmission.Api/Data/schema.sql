-- School Admission System schema.
-- Idempotent: safe to run on every startup and by hand in pgAdmin.

CREATE TABLE IF NOT EXISTS classes (
    id           uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    name         text        NOT NULL CHECK (length(trim(name)) > 0),
    total_seats  integer     NOT NULL CHECK (total_seats > 0),
    created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS holds (
    id           uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    class_id     uuid        NOT NULL REFERENCES classes(id),
    student_id   char(17)    NOT NULL CHECK (student_id ~ '^[0-9]{17}$'),
    status       text        NOT NULL DEFAULT 'HELD'
                             CHECK (status IN ('HELD','CONFIRMED','EXPIRED','CANCELLED')),
    created_at   timestamptz NOT NULL DEFAULT now(),
    expires_at   timestamptz NOT NULL,
    confirmed_at timestamptz NULL,
    cancelled_at timestamptz NULL
);

-- Seat counting per class.
CREATE INDEX IF NOT EXISTS ix_holds_class_status ON holds (class_id, status);

-- GET /me/holds.
CREATE INDEX IF NOT EXISTS ix_holds_student ON holds (student_id);

-- Expiry sweeper only scans HELD rows.
CREATE INDEX IF NOT EXISTS ix_holds_expiring ON holds (expires_at) WHERE status = 'HELD';

-- Rule 3: at most one active (HELD/CONFIRMED) hold per student per class.
CREATE UNIQUE INDEX IF NOT EXISTS ux_holds_active
    ON holds (class_id, student_id)
    WHERE status IN ('HELD','CONFIRMED');
