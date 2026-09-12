CREATE TABLE IF NOT EXISTS session_corrections (
    session_id TEXT PRIMARY KEY,
    corrected_started_at_utc TEXT NULL,
    corrected_ended_at_utc TEXT NULL,
    corrected_at_utc TEXT NOT NULL,
    CHECK (
        corrected_started_at_utc IS NOT NULL
        OR corrected_ended_at_utc IS NOT NULL
    ),
    FOREIGN KEY (session_id)
        REFERENCES game_sessions(session_id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_session_corrections_corrected_at
    ON session_corrections(corrected_at_utc DESC);
