-- Players. `id` is a client-generated UUID that the client stores locally and
-- sends on every request; `display_name` is the only thing shown publicly.
CREATE TABLE IF NOT EXISTS players (
  id            TEXT PRIMARY KEY,
  display_name  TEXT NOT NULL,
  created_at    INTEGER NOT NULL,
  last_seen_at  INTEGER NOT NULL,
  total_runs    INTEGER NOT NULL DEFAULT 0,
  best_score    INTEGER NOT NULL DEFAULT 0,
  banned        INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_players_best_score ON players (best_score DESC);

-- One row per completed run (win or death). This is the raw event log; the
-- leaderboard is derived from it so a ban can retroactively remove scores.
-- The client mints `id`, so a retried submission collides on the primary key
-- instead of double-scoring.
CREATE TABLE IF NOT EXISTS runs (
  id             TEXT PRIMARY KEY,
  player_id      TEXT NOT NULL REFERENCES players(id),
  mission_id     TEXT NOT NULL,
  score          INTEGER NOT NULL,
  duration_ms    INTEGER NOT NULL,
  kills          INTEGER NOT NULL,
  shots_fired    INTEGER NOT NULL,
  shots_hit      INTEGER NOT NULL,
  damage_taken   INTEGER NOT NULL,
  peak_alert     INTEGER NOT NULL,
  outcome        TEXT NOT NULL CHECK (outcome IN ('extracted', 'died', 'aborted')),
  client_version TEXT NOT NULL,
  platform       TEXT NOT NULL,
  submitted_at   INTEGER NOT NULL,
  -- Anti-cheat bookkeeping. `flags` is a comma-separated list of rule names
  -- that tripped; a flagged run is stored but excluded from the leaderboard.
  flags          TEXT NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS idx_runs_leaderboard
  ON runs (mission_id, score DESC, duration_ms ASC);
CREATE INDEX IF NOT EXISTS idx_runs_player ON runs (player_id, submitted_at DESC);

CREATE TABLE IF NOT EXISTS telemetry_events (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  player_id  TEXT,
  kind       TEXT NOT NULL,
  payload    TEXT NOT NULL,
  created_at INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_telemetry_kind_time ON telemetry_events (kind, created_at DESC);
