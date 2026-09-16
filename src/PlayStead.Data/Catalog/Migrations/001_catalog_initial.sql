PRAGMA foreign_keys = ON;

CREATE TABLE catalog_schema_migrations(
    version INTEGER PRIMARY KEY,
    applied_utc TEXT NOT NULL
);

CREATE TABLE catalog_metadata(
    singleton_id INTEGER PRIMARY KEY CHECK(singleton_id = 1),
    catalog_version INTEGER NOT NULL CHECK(catalog_version >= 0),
    generated_at_utc TEXT NOT NULL
);

INSERT INTO catalog_metadata(singleton_id, catalog_version, generated_at_utc)
VALUES(1, 0, '1970-01-01T00:00:00.0000000+00:00');

CREATE TABLE catalog_contents(
    internal_content_id TEXT PRIMARY KEY,
    public_id TEXT NOT NULL UNIQUE,
    content_kind INTEGER NOT NULL CHECK(content_kind IN (1, 2)),
    canonical_title TEXT NOT NULL,
    normalized_title TEXT NOT NULL,
    release_date TEXT NULL,
    developer TEXT NULL,
    publisher TEXT NULL,
    status INTEGER NOT NULL CHECK(status IN (1, 2, 3)),
    redirect_target_id TEXT NULL,
    FOREIGN KEY(redirect_target_id) REFERENCES catalog_contents(internal_content_id),
    CHECK((status = 2 AND redirect_target_id IS NOT NULL) OR (status IN (1, 3) AND redirect_target_id IS NULL)),
    CHECK(redirect_target_id IS NULL OR redirect_target_id <> internal_content_id)
);

CREATE INDEX ix_catalog_contents_normalized_title ON catalog_contents(normalized_title);
CREATE INDEX ix_catalog_contents_redirect_target ON catalog_contents(redirect_target_id);

CREATE TABLE catalog_provider_refs(
    provider INTEGER NOT NULL,
    external_id TEXT NOT NULL,
    content_id TEXT NOT NULL,
    external_type TEXT NULL,
    provenance INTEGER NOT NULL,
    confidence INTEGER NOT NULL,
    observed_at_utc TEXT NOT NULL,
    PRIMARY KEY(provider, external_id),
    FOREIGN KEY(content_id) REFERENCES catalog_contents(internal_content_id) ON DELETE CASCADE
);

CREATE INDEX ix_catalog_provider_refs_content ON catalog_provider_refs(content_id);

CREATE TABLE catalog_aliases(
    content_id TEXT NOT NULL,
    alias TEXT NOT NULL,
    normalized_alias TEXT NOT NULL,
    provenance INTEGER NOT NULL,
    PRIMARY KEY(content_id, alias),
    FOREIGN KEY(content_id) REFERENCES catalog_contents(internal_content_id) ON DELETE CASCADE
);

CREATE INDEX ix_catalog_aliases_normalized ON catalog_aliases(normalized_alias);

CREATE TABLE catalog_relations(
    source_content_id TEXT NOT NULL,
    relation_kind INTEGER NOT NULL,
    target_content_id TEXT NOT NULL,
    provenance INTEGER NOT NULL,
    confidence INTEGER NOT NULL,
    PRIMARY KEY(source_content_id, relation_kind, target_content_id),
    FOREIGN KEY(source_content_id) REFERENCES catalog_contents(internal_content_id) ON DELETE CASCADE,
    FOREIGN KEY(target_content_id) REFERENCES catalog_contents(internal_content_id) ON DELETE CASCADE,
    CHECK(source_content_id <> target_content_id)
);

CREATE INDEX ix_catalog_relations_target ON catalog_relations(target_content_id);
