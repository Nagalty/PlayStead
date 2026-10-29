PRAGMA foreign_keys=ON;

CREATE TABLE IF NOT EXISTS local_file_snapshots (
    snapshot_id TEXT PRIMARY KEY,
    installation_id TEXT NOT NULL,
    environment_entry_id TEXT NOT NULL,
    original_path TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    reason INTEGER NOT NULL,
    source_last_modified_utc TEXT NULL,
    source_size_bytes INTEGER NOT NULL,
    sha256 TEXT NOT NULL,
    snapshot_relative_path TEXT NOT NULL,
    is_pre_restore_backup INTEGER NOT NULL,
    metadata_json TEXT NULL,
    FOREIGN KEY (installation_id) REFERENCES installations(installation_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_local_file_snapshots_installation
    ON local_file_snapshots(installation_id, created_utc DESC);
CREATE INDEX IF NOT EXISTS ix_local_file_snapshots_entry
    ON local_file_snapshots(environment_entry_id, created_utc DESC);
