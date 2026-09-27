CREATE TABLE IF NOT EXISTS provider_activity_metadata (
    game_id TEXT NOT NULL,
    provider INTEGER NOT NULL,
    provider_game_id TEXT NOT NULL,
    total_playtime_seconds INTEGER NULL CHECK (total_playtime_seconds IS NULL OR total_playtime_seconds >= 0),
    last_played_utc TEXT NULL,
    refreshed_utc TEXT NOT NULL,
    availability INTEGER NOT NULL,
    PRIMARY KEY (game_id, provider),
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE
);
