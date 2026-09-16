CREATE TABLE notifications (
    notification_id TEXT NOT NULL PRIMARY KEY,
    producer INTEGER NOT NULL
        CHECK (producer IN (1)),
    subject_id TEXT NOT NULL,
    reason TEXT NOT NULL,
    deduplication_key TEXT NOT NULL UNIQUE,
    priority INTEGER NOT NULL
        CHECK (priority IN (1, 2, 3)),
    state INTEGER NOT NULL
        CHECK (state IN (1, 2, 3)),
    title TEXT NOT NULL,
    message TEXT NOT NULL,
    payload_json TEXT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    read_utc TEXT NULL,
    resolved_utc TEXT NULL,
    CHECK (
        (state = 1 AND read_utc IS NULL AND resolved_utc IS NULL)
        OR (state = 2 AND read_utc IS NOT NULL AND resolved_utc IS NULL)
        OR (state = 3 AND resolved_utc IS NOT NULL)
    )
);

CREATE INDEX ix_notifications_state
ON notifications(state);

CREATE INDEX ix_notifications_state_resolved_utc
ON notifications(state, resolved_utc);
