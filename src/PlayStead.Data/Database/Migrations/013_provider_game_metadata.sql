CREATE TABLE IF NOT EXISTS provider_game_metadata (
    game_id TEXT NOT NULL,
    provider INTEGER NOT NULL,
    provider_game_id TEXT NOT NULL,
    genres_json TEXT NULL,
    categories_json TEXT NULL,
    developers_json TEXT NULL,
    publishers_json TEXT NULL,
    release_date TEXT NULL,
    is_free INTEGER NULL,
    single_player INTEGER NULL,
    multi_player INTEGER NULL,
    online_coop INTEGER NULL,
    local_coop INTEGER NULL,
    refreshed_utc TEXT NOT NULL,
    availability INTEGER NOT NULL,
    PRIMARY KEY (game_id, provider),
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_provider_game_metadata_external
    ON provider_game_metadata(provider, provider_game_id);
