CREATE TABLE IF NOT EXISTS mod_evidence(
    game_id TEXT NOT NULL,
    provider INTEGER NOT NULL,
    detector_id TEXT NOT NULL,
    evidence_kind INTEGER NOT NULL,
    state INTEGER NOT NULL,
    observed_at_utc TEXT NOT NULL,
    detail TEXT NULL,
    PRIMARY KEY(game_id, detector_id, evidence_kind));

CREATE INDEX IF NOT EXISTS ix_mod_evidence_game
    ON mod_evidence(game_id, observed_at_utc DESC);
