ALTER TABLE games
ADD COLUMN canonical_content_id TEXT NULL;

CREATE INDEX IF NOT EXISTS
    ix_games_canonical_content_id
ON games(canonical_content_id);
