CREATE TABLE IF NOT EXISTS steam_local_evidence (
    app_id TEXT NOT NULL PRIMARY KEY,
    build_id TEXT NULL,
    branch_name TEXT NULL,
    depot_manifests_json TEXT NOT NULL,
    observed_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS steam_remote_evidence (
    app_id TEXT NOT NULL,
    branch_name TEXT NOT NULL,
    build_id TEXT NULL,
    depot_manifests_json TEXT NOT NULL,
    observed_at_utc TEXT NOT NULL,
    source INTEGER NOT NULL,
    schema_version INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (app_id, branch_name)
);

CREATE INDEX IF NOT EXISTS ix_steam_remote_evidence_observed_at_utc
    ON steam_remote_evidence(observed_at_utc);
