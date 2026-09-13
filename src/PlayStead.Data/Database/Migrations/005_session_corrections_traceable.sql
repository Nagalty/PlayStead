ALTER TABLE session_corrections
RENAME TO session_corrections_v4;

CREATE TABLE session_corrections (
    correction_id TEXT NOT NULL PRIMARY KEY,
    session_id TEXT NOT NULL UNIQUE,
    corrected_started_at_utc TEXT NULL,
    corrected_ended_at_utc TEXT NULL,
    reason TEXT NULL,
    created_at_utc TEXT NOT NULL,
    CHECK (
        corrected_started_at_utc IS NOT NULL
        OR corrected_ended_at_utc IS NOT NULL
    ),
    FOREIGN KEY (session_id)
        REFERENCES game_sessions(session_id)
        ON DELETE CASCADE
);

INSERT INTO session_corrections(
    correction_id,
    session_id,
    corrected_started_at_utc,
    corrected_ended_at_utc,
    reason,
    created_at_utc)
SELECT
    session_id,
    session_id,
    corrected_started_at_utc,
    corrected_ended_at_utc,
    NULL,
    corrected_at_utc
FROM session_corrections_v4;

DROP TABLE session_corrections_v4;

CREATE INDEX ix_session_corrections_created_at
    ON session_corrections(created_at_utc DESC);
