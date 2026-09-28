CREATE TABLE IF NOT EXISTS local_artifact_baselines(
    game_id TEXT NOT NULL,
    artifact_kind INTEGER NOT NULL,
    artifact_identity TEXT NOT NULL,
    algorithm TEXT NOT NULL,
    hash TEXT NOT NULL,
    file_count INTEGER NOT NULL,
    total_size_bytes INTEGER NOT NULL,
    captured_at_utc TEXT NOT NULL,
    PRIMARY KEY(game_id, artifact_kind, artifact_identity),
    FOREIGN KEY(game_id) REFERENCES games(game_id) ON DELETE CASCADE
);
