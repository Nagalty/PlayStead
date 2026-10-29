PRAGMA foreign_keys=ON;

CREATE TABLE IF NOT EXISTS local_environment_baselines (
    installation_id TEXT PRIMARY KEY,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    FOREIGN KEY (installation_id) REFERENCES installations(installation_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS local_environment_baseline_entries (
    installation_id TEXT NOT NULL,
    kind INTEGER NOT NULL,
    normalized_path TEXT NOT NULL,
    component_kind TEXT NOT NULL DEFAULT '',
    component_name TEXT NOT NULL DEFAULT '',
    path_kind INTEGER NOT NULL,
    exists_flag INTEGER NOT NULL,
    last_modified_utc TEXT NULL,
    metadata_json TEXT NULL,
    PRIMARY KEY (installation_id, kind, normalized_path, component_kind, component_name),
    FOREIGN KEY (installation_id) REFERENCES local_environment_baselines(installation_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_local_environment_baseline_entries_installation
    ON local_environment_baseline_entries(installation_id);
