CREATE TABLE projects (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL,
  completed_at TEXT,
  created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- project_id NULL = loose list
CREATE TABLE lists (
  id INTEGER PRIMARY KEY,
  project_id INTEGER REFERENCES projects(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  position INTEGER NOT NULL DEFAULT 0,
  archived_at TEXT,
  created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- list_id NULL = task captured straight into Today, in no list
-- today_date NULL = backlog; dates are local days, 'YYYY-MM-DD'
CREATE TABLE tasks (
  id INTEGER PRIMARY KEY,
  list_id INTEGER REFERENCES lists(id) ON DELETE CASCADE,
  parent_id INTEGER REFERENCES tasks(id) ON DELETE CASCADE,
  title TEXT NOT NULL,
  notes TEXT NOT NULL DEFAULT '',
  priority INTEGER NOT NULL DEFAULT 1,
  tags TEXT NOT NULL DEFAULT '',
  planned_date TEXT,
  due_date TEXT,
  pomodoro_enabled INTEGER NOT NULL DEFAULT 0,
  today_date TEXT,
  position INTEGER NOT NULL DEFAULT 0,
  today_position INTEGER NOT NULL DEFAULT 0,
  done_at TEXT,
  postpone_count INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX tasks_list ON tasks(list_id);
CREATE INDEX tasks_parent ON tasks(parent_id);
CREATE INDEX tasks_today ON tasks(today_date);
