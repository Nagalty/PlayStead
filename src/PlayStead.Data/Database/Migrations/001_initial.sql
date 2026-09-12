PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS schema_migrations (
    version INTEGER PRIMARY KEY,
    applied_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS games (
    game_id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    is_hidden INTEGER NOT NULL DEFAULT 0 CHECK (is_hidden IN (0, 1)),
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS provider_game_refs (
    provider INTEGER NOT NULL,
    external_id TEXT NOT NULL,
    game_id TEXT NOT NULL,
    PRIMARY KEY (provider, external_id),
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS installations (
    installation_id TEXT PRIMARY KEY,
    game_id TEXT NOT NULL,
    provider INTEGER NOT NULL,
    external_id TEXT NOT NULL,
    install_path TEXT NOT NULL,
    installed_size_bytes INTEGER NULL CHECK (installed_size_bytes IS NULL OR installed_size_bytes >= 0),
    is_preferred INTEGER NOT NULL DEFAULT 0 CHECK (is_preferred IN (0, 1)),
    is_present INTEGER NOT NULL DEFAULT 1 CHECK (is_present IN (0, 1)),
    last_seen_utc TEXT NOT NULL,
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_installations_source_path
    ON installations(provider, external_id, install_path);

CREATE INDEX IF NOT EXISTS ix_installations_game_id
    ON installations(game_id);
