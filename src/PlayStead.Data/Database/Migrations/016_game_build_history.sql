CREATE TABLE IF NOT EXISTS game_build_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    game_id TEXT NOT NULL,
    provider INTEGER NOT NULL,
    provider_game_id TEXT NOT NULL,
    build_id TEXT NOT NULL,
    observed_at_utc TEXT NOT NULL,
    source INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_game_build_history_game_observed
    ON game_build_history(game_id, observed_at_utc);

CREATE INDEX IF NOT EXISTS ix_game_build_history_provider_external
    ON game_build_history(provider, provider_game_id);
