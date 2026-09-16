CREATE TABLE game_identity_resolutions (
    game_id TEXT NOT NULL PRIMARY KEY,
    provisional_id TEXT NULL,
    state INTEGER NOT NULL
        CHECK (state IN (1, 2, 3, 4)),
    candidate_content_id TEXT NULL,
    evidence_json TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,

    FOREIGN KEY (game_id)
        REFERENCES games(game_id)
        ON DELETE CASCADE,

    CHECK (
        state <> 4
        OR candidate_content_id IS NULL
    ),

    CHECK (
        state <> 1
        OR candidate_content_id IS NOT NULL
    )
);

CREATE UNIQUE INDEX
    ux_game_identity_resolutions_provisional
ON game_identity_resolutions(provisional_id)
WHERE provisional_id IS NOT NULL;

CREATE INDEX
    ix_game_identity_resolutions_candidate
ON game_identity_resolutions(candidate_content_id);
