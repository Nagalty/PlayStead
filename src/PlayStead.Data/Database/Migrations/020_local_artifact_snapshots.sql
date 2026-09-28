CREATE TABLE IF NOT EXISTS local_artifact_snapshots(
    snapshot_id TEXT PRIMARY KEY,
    game_id TEXT NOT NULL,
    artifact_kind INTEGER NOT NULL,
    rule_identity TEXT NOT NULL,
    archive_path TEXT NOT NULL,
    fingerprint_algorithm TEXT NOT NULL,
    fingerprint_hash TEXT NOT NULL,
    file_count INTEGER NOT NULL,
    total_size_bytes INTEGER NOT NULL,
    created_at_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_local_artifact_snapshots_lookup
ON local_artifact_snapshots(game_id, artifact_kind, rule_identity, created_at_utc DESC);
