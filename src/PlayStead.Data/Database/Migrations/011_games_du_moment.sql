CREATE TABLE IF NOT EXISTS games_du_moment(
    game_id TEXT NOT NULL PRIMARY KEY,
    position INTEGER NOT NULL UNIQUE,
    added_at_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_games_du_moment_position ON games_du_moment(position);
