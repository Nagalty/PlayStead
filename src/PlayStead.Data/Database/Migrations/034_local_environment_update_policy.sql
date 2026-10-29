CREATE TABLE IF NOT EXISTS local_environment_update_policies (
    installation_id TEXT PRIMARY KEY,
    policy INTEGER NOT NULL DEFAULT 1,
    updated_utc TEXT NOT NULL,
    FOREIGN KEY (installation_id) REFERENCES installations(installation_id) ON DELETE CASCADE
);
