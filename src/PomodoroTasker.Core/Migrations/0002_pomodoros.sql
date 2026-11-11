-- One row per focus interval. task_title and project_name are copies, so that reports
-- survive task edits and deletions. day is the local day on which the interval ended.
CREATE TABLE pomodoros (
  id INTEGER PRIMARY KEY,
  task_id INTEGER REFERENCES tasks(id) ON DELETE SET NULL,
  task_title TEXT NOT NULL,
  project_name TEXT,
  day TEXT NOT NULL,
  started_at TEXT NOT NULL,
  ended_at TEXT NOT NULL,
  seconds INTEGER NOT NULL,
  status TEXT NOT NULL CHECK (status IN ('completed', 'interrupted')),
  reason TEXT NOT NULL DEFAULT ''
);

CREATE INDEX pomodoros_day ON pomodoros(day);
CREATE INDEX pomodoros_task ON pomodoros(task_id);

-- Small key/value store: the running timer state now, user settings later.
CREATE TABLE settings (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL
);
