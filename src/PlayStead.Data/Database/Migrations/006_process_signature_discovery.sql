ALTER TABLE process_signature_entries ADD COLUMN executable_path TEXT NULL;
ALTER TABLE process_signature_entries ADD COLUMN validated_size_bytes INTEGER NULL
    CHECK (validated_size_bytes IS NULL OR validated_size_bytes >= 0);
ALTER TABLE process_signature_entries ADD COLUMN validated_last_write_utc TEXT NULL;

CREATE UNIQUE INDEX ux_installations_identity_game
    ON installations(installation_id, game_id);

CREATE TABLE process_signature_validation (
    game_id TEXT NOT NULL PRIMARY KEY,
    installation_id TEXT NULL,
    generation_id TEXT NULL,
    policy_version INTEGER NULL CHECK (policy_version IS NULL OR policy_version > 0),
    validation_state INTEGER NOT NULL DEFAULT 0 CHECK (validation_state IN (0, 1)),
    concurrency_token TEXT NOT NULL CHECK (length(concurrency_token) = 32),
    FOREIGN KEY (game_id) REFERENCES process_signatures(game_id) ON DELETE CASCADE,
    FOREIGN KEY (installation_id) REFERENCES installations(installation_id) ON DELETE SET NULL
);

CREATE INDEX ix_process_signature_validation_installation
    ON process_signature_validation(installation_id);

INSERT INTO process_signature_validation(game_id, validation_state, concurrency_token)
SELECT game_id, 0, lower(hex(randomblob(16)))
FROM process_signatures WHERE origin = 0;

CREATE TABLE process_signature_learning (
    installation_id TEXT NOT NULL PRIMARY KEY,
    game_id TEXT NOT NULL,
    root_path TEXT NOT NULL,
    generation_id TEXT NOT NULL,
    policy_version INTEGER NOT NULL CHECK (policy_version > 0),
    concurrency_token TEXT NOT NULL CHECK (length(concurrency_token) = 32),
    last_sequence_number INTEGER NOT NULL DEFAULT 0 CHECK (last_sequence_number >= 0),
    has_ambiguous_installation INTEGER NOT NULL CHECK (has_ambiguous_installation IN (0, 1)),
    inventory_json TEXT NOT NULL CHECK (json_valid(inventory_json)),
    reference_json TEXT NULL CHECK (reference_json IS NULL OR json_valid(reference_json)),
    confirmation_json TEXT NULL CHECK (confirmation_json IS NULL OR json_valid(confirmation_json)),
    reasons_json TEXT NOT NULL CHECK (json_valid(reasons_json)),
    CHECK (confirmation_json IS NULL OR reference_json IS NOT NULL),
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE,
    FOREIGN KEY (installation_id, game_id)
        REFERENCES installations(installation_id, game_id) ON DELETE CASCADE
);

CREATE INDEX ix_process_signature_learning_game
    ON process_signature_learning(game_id);

CREATE TRIGGER process_signature_validation_installation_deleted
AFTER UPDATE OF installation_id ON process_signature_validation
WHEN OLD.installation_id IS NOT NULL AND NEW.installation_id IS NULL
BEGIN
    UPDATE process_signature_validation
    SET validation_state = 0, concurrency_token = lower(hex(randomblob(16)))
    WHERE game_id = NEW.game_id;
END;
