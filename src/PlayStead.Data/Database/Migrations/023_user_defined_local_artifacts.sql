CREATE TABLE IF NOT EXISTS user_defined_local_artifacts(
    artifact_id TEXT PRIMARY KEY,
    game_id TEXT NOT NULL,
    artifact_kind INTEGER NOT NULL,
    path TEXT NOT NULL,
    display_name TEXT NULL,
    created_at_utc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_user_defined_local_artifacts_game
ON user_defined_local_artifacts(game_id, created_at_utc DESC);
