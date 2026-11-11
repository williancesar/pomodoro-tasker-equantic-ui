-- A recurrence is a rule on a task. The same task row is reopened on each due day, so its
-- snapshots and pomodoros (both keyed by day) are the record of every occurrence.
-- rec_days is a bitmask, Sunday = 1, Monday = 2, ... Saturday = 64. Used when rec_freq = 'weekly'.
ALTER TABLE tasks ADD COLUMN rec_freq TEXT CHECK (rec_freq IN ('daily', 'weekly'));
ALTER TABLE tasks ADD COLUMN rec_interval INTEGER NOT NULL DEFAULT 1;
ALTER TABLE tasks ADD COLUMN rec_days INTEGER NOT NULL DEFAULT 0;
ALTER TABLE tasks ADD COLUMN rec_anchor TEXT;
ALTER TABLE tasks ADD COLUMN rec_paused INTEGER NOT NULL DEFAULT 0;
-- The last due day already handled, so a day never produces two occurrences.
ALTER TABLE tasks ADD COLUMN rec_last TEXT;
