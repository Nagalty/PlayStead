CREATE TABLE game_identity_decisions(
    decision_id TEXT NOT NULL PRIMARY KEY,
    game_id TEXT NOT NULL,
    catalog_content_id TEXT NOT NULL,
    decision_type INTEGER NOT NULL CHECK(decision_type IN (1, 2)),
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    revoked_utc TEXT NULL,
    FOREIGN KEY(game_id) REFERENCES games(game_id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX ux_game_identity_decisions_active_confirm
ON game_identity_decisions(game_id)
WHERE decision_type = 1 AND revoked_utc IS NULL;

CREATE UNIQUE INDEX ux_game_identity_decisions_active_candidate
ON game_identity_decisions(game_id, catalog_content_id)
WHERE revoked_utc IS NULL;
