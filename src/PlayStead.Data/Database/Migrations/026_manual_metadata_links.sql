CREATE TABLE manual_metadata_links(
    game_id TEXT PRIMARY KEY,
    canonical_content_id TEXT NOT NULL,
    media_provider INTEGER NULL,
    media_external_id TEXT NULL,
    matched_at_utc TEXT NOT NULL
);
