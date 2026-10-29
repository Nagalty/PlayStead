PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS local_environment_entries (
    entry_id TEXT PRIMARY KEY,
    installation_id TEXT NOT NULL,
    kind INTEGER NOT NULL,
    path TEXT NOT NULL,
    path_kind INTEGER NOT NULL,
    confidence INTEGER NOT NULL,
    source INTEGER NOT NULL,
    exists_flag INTEGER NOT NULL CHECK (exists_flag IN (0, 1)),
    last_observed_utc TEXT NOT NULL,
    last_modified_utc TEXT NULL,
    metadata_json TEXT NULL,
    is_user_override INTEGER NOT NULL DEFAULT 0 CHECK (is_user_override IN (0, 1)),
    FOREIGN KEY (installation_id) REFERENCES installations(installation_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_local_environment_installation
    ON local_environment_entries(installation_id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_local_environment_detected
    ON local_environment_entries(installation_id, kind, path, is_user_override);
