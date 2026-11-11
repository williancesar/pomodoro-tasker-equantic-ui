-- A closed day. Present even when nothing was in Today that day.
CREATE TABLE days (
  day TEXT PRIMARY KEY,
  closed_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- The immutable record of a closed day: one row per task that was in Today.
-- Titles and names are copies, so later edits and deletions do not change history.
CREATE TABLE day_snapshots (
  id INTEGER PRIMARY KEY,
  day TEXT NOT NULL,
  task_id INTEGER REFERENCES tasks(id) ON DELETE SET NULL,
  task_title TEXT NOT NULL,
  project_name TEXT,
  list_name TEXT,
  outcome TEXT NOT NULL CHECK (outcome IN ('done', 'postponed')),
  tomatoes INTEGER NOT NULL DEFAULT 0,
  interrupted INTEGER NOT NULL DEFAULT 0,
  UNIQUE (day, task_id)
);

CREATE INDEX day_snapshots_day ON day_snapshots(day);
