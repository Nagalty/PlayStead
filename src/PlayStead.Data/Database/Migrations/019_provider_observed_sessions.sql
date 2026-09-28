CREATE TABLE IF NOT EXISTS provider_observed_sessions (
    session_id TEXT PRIMARY KEY,
    game_id TEXT NOT NULL,
    provider INTEGER NOT NULL,
    provider_game_id TEXT NOT NULL,
    started_at_utc TEXT NULL,
    ended_at_utc TEXT NULL,
    source TEXT NOT NULL,
    completeness INTEGER NOT NULL,
    UNIQUE(provider, provider_game_id, started_at_utc, source),
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_provider_observed_sessions_period
    ON provider_observed_sessions(started_at_utc, ended_at_utc);
