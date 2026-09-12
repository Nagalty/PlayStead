CREATE TABLE IF NOT EXISTS game_sessions (
    session_id TEXT PRIMARY KEY,
    game_id TEXT NOT NULL,
    observed_started_at_utc TEXT NOT NULL,
    last_seen_at_utc TEXT NOT NULL,
    observed_ended_at_utc TEXT NULL,
    state INTEGER NOT NULL CHECK (state IN (0, 1, 2)),
    end_reason INTEGER NULL CHECK (end_reason IS NULL OR end_reason IN (0, 1, 2, 3)),
    detection_source INTEGER NOT NULL CHECK (detection_source IN (0)),
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_game_sessions_state
    ON game_sessions(state);

CREATE INDEX IF NOT EXISTS ix_game_sessions_game_started
    ON game_sessions(game_id, observed_started_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_game_sessions_started
    ON game_sessions(observed_started_at_utc DESC);

CREATE TABLE IF NOT EXISTS process_signatures (
    game_id TEXT PRIMARY KEY,
    origin INTEGER NOT NULL CHECK (origin IN (0, 1, 2)),
    updated_at_utc TEXT NOT NULL,
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS process_signature_entries (
    game_id TEXT NOT NULL,
    ordinal INTEGER NOT NULL CHECK (ordinal >= 0),
    executable_name TEXT NOT NULL,
    kind INTEGER NOT NULL CHECK (kind IN (0, 1, 2)),
    PRIMARY KEY (game_id, ordinal),
    FOREIGN KEY (game_id) REFERENCES process_signatures(game_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_process_signature_entries_executable_name
    ON process_signature_entries(executable_name COLLATE NOCASE);
