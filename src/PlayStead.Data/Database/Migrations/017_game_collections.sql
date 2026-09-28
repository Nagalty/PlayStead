CREATE TABLE IF NOT EXISTS game_collections(
    collection_id TEXT PRIMARY KEY,
    name TEXT NOT NULL COLLATE NOCASE UNIQUE,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS game_collection_memberships(
    collection_id TEXT NOT NULL,
    game_id TEXT NOT NULL,
    PRIMARY KEY (collection_id, game_id),
    FOREIGN KEY (collection_id) REFERENCES game_collections(collection_id) ON DELETE CASCADE,
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_game_collection_memberships_game_id
    ON game_collection_memberships(game_id);
