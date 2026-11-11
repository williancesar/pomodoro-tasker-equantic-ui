using System.Text.Json;

namespace PomodoroTasker.Core;

/// <summary>All SQL lives here, the same statements the desktop app runs.</summary>
public static class Repo
{
    private const string TaskSelect = """
        SELECT t.*, l.name AS list_name, p.name AS project_name, pt.title AS parent_title
        FROM tasks t
        LEFT JOIN lists l ON l.id = t.list_id
        LEFT JOIN projects p ON p.id = l.project_id
        LEFT JOIN tasks pt ON pt.id = t.parent_id
        """;

    private static string NowIso() => Day.Iso(DateTimeOffset.UtcNow);

    /// <summary>Puts each sub-task under its parent when the parent is in the same result; keeps row order.</summary>
    public static List<TaskItem> Nest(List<TaskItem> rows)
    {
        var byId = rows.ToDictionary(r => r.Id);
        var top = new List<TaskItem>();
        foreach (var t in rows)
        {
            if (t.ParentId != null && byId.TryGetValue(t.ParentId.Value, out var parent)) parent.Subs.Add(t);
            else top.Add(t);
        }
        return top;
    }

    // ---------- tasks ----------

    public static List<TaskItem> TodayTasks(Db db, string day) => Nest(db.Select<TaskItem>(
        $"""
         {TaskSelect}
         WHERE t.today_date = $1 OR t.parent_id IN (SELECT id FROM tasks WHERE today_date = $1)
         ORDER BY t.today_position, t.position, t.id
         """, day));

    public static List<TaskItem> ListTasks(Db db, int listId) =>
        Nest(db.Select<TaskItem>($"{TaskSelect} WHERE t.list_id = $1 ORDER BY t.position, t.id", listId));

    public static TaskItem? GetTask(Db db, int id) =>
        Nest(db.Select<TaskItem>($"{TaskSelect} WHERE t.id = $1 OR t.parent_id = $1 ORDER BY t.position, t.id", id))
            .Find(t => t.Id == id);

    public static int CreateTask(Db db, string title, int? listId = null, int? parentId = null, string? todayDate = null) =>
        // One growing counter for both orders: only relative order matters.
        db.Execute(
            """
            INSERT INTO tasks (title, list_id, parent_id, today_date, position, today_position)
            VALUES ($1,
                    COALESCE((SELECT list_id FROM tasks WHERE id = $3), $2),
                    $3, $4,
                    (SELECT COALESCE(MAX(position), 0) + 1 FROM tasks),
                    (SELECT COALESCE(MAX(today_position), 0) + 1 FROM tasks))
            """, title.Trim(), listId, parentId, todayDate);

    private static readonly string[] Patchable =
        ["title", "notes", "priority", "tags", "planned_date", "due_date", "pomodoro_enabled", "list_id"];

    /// <summary>Updates the columns named in the patch; any other key is ignored.</summary>
    public static void UpdateTask(Db db, int id, Dictionary<string, object?> patch)
    {
        var keys = Patchable.Where(patch.ContainsKey).ToList();
        if (keys.Count == 0) return;
        db.Execute(
            $"UPDATE tasks SET {string.Join(", ", keys.Select((k, i) => $"{k} = ${i + 2}"))} WHERE id = $1",
            [id, .. keys.Select(k => patch[k])]);
        // Sub-tasks always live in their parent's list.
        if (patch.TryGetValue("list_id", out var listId))
            db.Execute("UPDATE tasks SET list_id = $2 WHERE parent_id = $1", id, listId);
    }

    /// <summary>
    /// Marks a task done or open. Done on a parent also closes its open sub-tasks.
    /// Returns the id of the loose list that was auto-archived because this was its last open task.
    /// </summary>
    public static int? SetDone(Db db, int id, bool done, string? now = null)
    {
        if (!done)
        {
            db.Execute("UPDATE tasks SET done_at = NULL WHERE id = $1", id);
            return null;
        }
        now ??= NowIso();
        db.Execute("UPDATE tasks SET done_at = $2 WHERE id = $1 OR (parent_id = $1 AND done_at IS NULL)", id, now);
        // A task finished straight from its list still belongs to the record of the day it was done.
        db.Execute("UPDATE tasks SET today_date = $2 WHERE id = $1 AND parent_id IS NULL AND today_date IS NULL", id, Day.OfIso(now));
        var list = db.Select<int>(
            """
            SELECT l.id FROM lists l JOIN tasks t ON t.list_id = l.id
            WHERE t.id = $1 AND l.project_id IS NULL AND l.archived_at IS NULL
              AND NOT EXISTS (SELECT 1 FROM tasks o WHERE o.list_id = l.id AND o.parent_id IS NULL AND o.done_at IS NULL)
            """, id);
        if (list.Count == 0) return null;
        db.Execute("UPDATE lists SET archived_at = $2 WHERE id = $1", list[0], now);
        return list[0];
    }

    /// <summary>day = pull into Today; null = send back to the backlog (which also drops the planned date).</summary>
    public static void SetToday(Db db, int id, string? day)
    {
        if (day != null)
            db.Execute(
                "UPDATE tasks SET today_date = $2, today_position = (SELECT COALESCE(MAX(today_position), 0) + 1 FROM tasks) WHERE id = $1",
                id, day);
        else
            db.Execute("UPDATE tasks SET today_date = NULL, planned_date = NULL WHERE id = $1", id);
    }

    /// <summary>Open backlog tasks whose planned date has arrived enter Today.</summary>
    public static void EnterPlanned(Db db, string day) =>
        db.Execute("UPDATE tasks SET today_date = $1 WHERE planned_date <= $1 AND today_date IS NULL AND done_at IS NULL", day);

    /// <param name="today">Orders Today (today_position) instead of the list (position).</param>
    public static void Reorder(Db db, IReadOnlyList<int> ids, bool today)
    {
        var column = today ? "today_position" : "position";
        for (var i = 0; i < ids.Count; i++) db.Execute($"UPDATE tasks SET {column} = $2 WHERE id = $1", ids[i], i + 1);
    }

    public static void DeleteTask(Db db, int id) => db.Execute("DELETE FROM tasks WHERE id = $1", id);

    // ---------- lists ----------

    private const string ListSelect = """
        SELECT l.*,
          (SELECT COUNT(*) FROM tasks t WHERE t.list_id = l.id AND t.parent_id IS NULL) AS total,
          (SELECT COUNT(*) FROM tasks t WHERE t.list_id = l.id AND t.parent_id IS NULL AND t.done_at IS NOT NULL) AS done,
          (SELECT COUNT(*) FROM tasks t WHERE t.list_id = l.id AND t.today_date = $1 AND t.done_at IS NULL) AS in_today
        FROM lists l
        """;

    public static List<TaskList> LooseLists(Db db, string day, bool archived) => db.Select<TaskList>(
        $"""
         {ListSelect} WHERE l.project_id IS NULL AND l.archived_at IS {(archived ? "NOT NULL" : "NULL")}
         ORDER BY {(archived ? "l.archived_at DESC" : "l.position, l.id")}
         """, day);

    public static List<TaskList> ProjectLists(Db db, string day, int projectId) =>
        db.Select<TaskList>($"{ListSelect} WHERE l.project_id = $2 ORDER BY l.position, l.id", day, projectId);

    public static TaskList? GetList(Db db, string day, int id) =>
        db.Select<TaskList>($"{ListSelect} WHERE l.id = $2", day, id).FirstOrDefault();

    /// <summary>Every list a task can be moved to: project lists and active loose lists.</summary>
    public static List<ListOption> ListOptions(Db db) => db.Select<ListOption>(
        """
        SELECT l.id, CASE WHEN p.id IS NULL THEN l.name ELSE p.name || ' › ' || l.name END AS label
        FROM lists l LEFT JOIN projects p ON p.id = l.project_id
        WHERE l.archived_at IS NULL AND (p.id IS NULL OR p.completed_at IS NULL)
        ORDER BY p.id IS NULL, p.name, l.position, l.id
        """);

    public static int CreateList(Db db, string name, int? projectId = null) => db.Execute(
        "INSERT INTO lists (name, project_id, position) VALUES ($1, $2, (SELECT COALESCE(MAX(position), 0) + 1 FROM lists))",
        name.Trim(), projectId);

    public static void RenameList(Db db, int id, string name) =>
        db.Execute("UPDATE lists SET name = $2 WHERE id = $1", id, name.Trim());

    public static void SetListArchived(Db db, int id, bool archived, string? now = null) =>
        db.Execute("UPDATE lists SET archived_at = $2 WHERE id = $1", id, archived ? now ?? NowIso() : null);

    public static void DeleteList(Db db, int id) => db.Execute("DELETE FROM lists WHERE id = $1", id);

    // ---------- projects ----------

    private const string ProjectSelect = """
        SELECT p.*,
          (SELECT COUNT(*) FROM lists l WHERE l.project_id = p.id) AS lists,
          (SELECT COUNT(*) FROM tasks t JOIN lists l ON l.id = t.list_id WHERE l.project_id = p.id AND t.parent_id IS NULL AND t.done_at IS NULL) AS open,
          (SELECT COUNT(*) FROM tasks t JOIN lists l ON l.id = t.list_id WHERE l.project_id = p.id AND t.parent_id IS NULL AND t.done_at IS NOT NULL) AS done,
          (SELECT COUNT(*) FROM tasks t JOIN lists l ON l.id = t.list_id WHERE l.project_id = p.id AND t.today_date = $1 AND t.done_at IS NULL) AS in_today,
          (SELECT COUNT(*) FROM tasks t JOIN lists l ON l.id = t.list_id WHERE l.project_id = p.id AND t.due_date < $1 AND t.done_at IS NULL) AS overdue
        FROM projects p
        """;

    public static List<Project> Projects(Db db, string day) =>
        db.Select<Project>($"{ProjectSelect} ORDER BY p.completed_at IS NOT NULL, p.completed_at DESC, p.id", day);

    public static Project? GetProject(Db db, string day, int id) =>
        db.Select<Project>($"{ProjectSelect} WHERE p.id = $2", day, id).FirstOrDefault();

    public static int CreateProject(Db db, string name) => db.Execute("INSERT INTO projects (name) VALUES ($1)", name.Trim());

    public static void RenameProject(Db db, int id, string name) =>
        db.Execute("UPDATE projects SET name = $2 WHERE id = $1", id, name.Trim());

    public static void SetProjectCompleted(Db db, int id, bool completed, string? now = null) =>
        db.Execute("UPDATE projects SET completed_at = $2 WHERE id = $1", id, completed ? now ?? NowIso() : null);

    public static void DeleteProject(Db db, int id) => db.Execute("DELETE FROM projects WHERE id = $1", id);

    // ---------- pomodoros ----------

    /// <summary>Stores a finished focus interval, with copies of the task title and project name.</summary>
    public static void AddPomodoro(Db db, PomodoroRecord r) => db.Execute(
        """
        INSERT INTO pomodoros (task_id, task_title, project_name, day, started_at, ended_at, seconds, status, reason)
        SELECT t.id, t.title, p.name, $2, $3, $4, $5, $6, $7
        FROM tasks t LEFT JOIN lists l ON l.id = t.list_id LEFT JOIN projects p ON p.id = l.project_id
        WHERE t.id = $1
        """,
        r.TaskId, Day.OfMs(r.EndedAt), Day.Iso(DateTimeOffset.FromUnixTimeMilliseconds(r.StartedAt)),
        Day.Iso(DateTimeOffset.FromUnixTimeMilliseconds(r.EndedAt)), r.Seconds, r.Status, r.Reason);

    /// <summary>Pomodoros per task on one day. A parent task also counts those of its sub-tasks.</summary>
    public static List<Tomatoes> Tomatoes(Db db, string day) => db.Select<Tomatoes>(
        """
        SELECT id, SUM(done) AS done, SUM(interrupted) AS interrupted FROM (
          SELECT t.id AS id, (p.status = 'completed') AS done, (p.status = 'interrupted') AS interrupted
          FROM pomodoros p JOIN tasks t ON t.id = p.task_id WHERE p.day = $1
          UNION ALL
          SELECT t.parent_id, (p.status = 'completed'), (p.status = 'interrupted')
          FROM pomodoros p JOIN tasks t ON t.id = p.task_id WHERE p.day = $1 AND t.parent_id IS NOT NULL
        ) GROUP BY id
        """, day);

    // ---------- settings ----------

    // camelCase, so a database the desktop app wrote reads the same.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static T? GetSetting<T>(Db db, string key) where T : class
    {
        var row = db.Select<string>("SELECT value FROM settings WHERE key = $1", key);
        return row.Count == 0 ? null : JsonSerializer.Deserialize<T>(row[0], Json);
    }

    public static void SetSetting<T>(Db db, string key, T value) => db.Execute(
        "INSERT INTO settings (key, value) VALUES ($1, $2) ON CONFLICT(key) DO UPDATE SET value = $2",
        key, JsonSerializer.Serialize(value, Json));

    // ---------- day close ----------

    /// <summary>
    /// Closes one day: writes its snapshot, then rolls every open Today task to the next day.
    /// Tasks in toBacklog leave Today instead and are not counted as postponed.
    /// Safe to run again on the same day: it only adds what is not recorded yet.
    /// </summary>
    public static void CloseDay(Db db, string day, IEnumerable<int>? toBacklog = null)
    {
        using var transaction = db.Begin();
        foreach (var id in toBacklog ?? []) SetToday(db, id, null);
        static string Count(string status) =>
            $"""
             (SELECT COUNT(*) FROM pomodoros x JOIN tasks xt ON xt.id = x.task_id
              WHERE x.day = $1 AND x.status = '{status}' AND (xt.id = t.id OR xt.parent_id = t.id))
             """;
        db.Execute(
            $"""
             INSERT OR IGNORE INTO day_snapshots (day, task_id, task_title, project_name, list_name, outcome, tomatoes, interrupted)
             SELECT $1, t.id, t.title, p.name, l.name, CASE WHEN t.done_at IS NULL THEN 'postponed' ELSE 'done' END,
                    {Count("completed")}, {Count("interrupted")}
             FROM tasks t LEFT JOIN lists l ON l.id = t.list_id LEFT JOIN projects p ON p.id = l.project_id
             WHERE t.today_date = $1
             """, day);
        db.Execute("INSERT OR IGNORE INTO days (day) VALUES ($1)", day);
        db.Execute("UPDATE tasks SET today_date = NULL WHERE today_date = $1 AND done_at IS NOT NULL", day);
        db.Execute(
            "UPDATE tasks SET today_date = $2, postpone_count = postpone_count + 1 WHERE today_date = $1 AND done_at IS NULL",
            day, Day.AddDays(day, 1));
        transaction.Commit();
    }

    /// <summary>
    /// Closes, in order, every day that ended before now. Returns the days closed by this call.
    /// The last closed day is closed again first: it may have been closed early by the user and
    /// gained tasks afterwards.
    /// </summary>
    public static List<string> CloseMissedDays(Db db, string now)
    {
        var last = db.Select<string?>("SELECT MAX(day) FROM days")[0];
        var first = db.Select<string?>("SELECT MIN(today_date) FROM tasks WHERE today_date < $1", now)[0];
        var closed = new List<string>();
        for (var d = last ?? first ?? now; string.CompareOrdinal(d, now) < 0; d = Day.AddDays(d, 1))
        {
            // Each missed day first receives its occurrences, so a routine nobody did shows as postponed.
            SpawnOccurrences(db, d);
            CloseDay(db, d);
            if (d != last) closed.Add(d);
        }
        SpawnOccurrences(db, now);
        return closed;
    }

    public static DaySummary DaySummary(Db db, string day)
    {
        var summary = db.Select<DaySummary>(
            """
            SELECT
              (SELECT COUNT(*) FROM day_snapshots WHERE day = $1 AND outcome = 'done') AS done,
              (SELECT COUNT(*) FROM day_snapshots WHERE day = $1 AND outcome = 'postponed') AS postponed,
              (SELECT COUNT(*) FROM pomodoros WHERE day = $1 AND status = 'completed') AS tomatoes,
              (SELECT COALESCE(SUM(seconds), 0) FROM pomodoros WHERE day = $1) AS focusSeconds
            """, day)[0];
        summary.Day = day;
        return summary;
    }

    public static bool IsClosed(Db db, string day) => db.Select<int>("SELECT 1 FROM days WHERE day = $1", day).Count > 0;

    /// <summary>A task's past, newest first: pomodoros per day and the days on which it was postponed.</summary>
    public static List<HistoryDay> TaskHistory(Db db, int taskId) => db.Select<HistoryDay>(
        """
        SELECT day, SUM(done) AS done, SUM(interrupted) AS interrupted, SUM(seconds) AS seconds,
               GROUP_CONCAT(NULLIF(reason, ''), ', ') AS reasons, SUM(postponed) AS postponed
        FROM (
          SELECT p.day, (p.status = 'completed') AS done, (p.status = 'interrupted') AS interrupted, p.seconds, p.reason, 0 AS postponed
          FROM pomodoros p JOIN tasks t ON t.id = p.task_id WHERE t.id = $1 OR t.parent_id = $1
          UNION ALL
          SELECT day, 0, 0, 0, '', 1 FROM day_snapshots WHERE task_id = $1 AND outcome = 'postponed'
        ) GROUP BY day ORDER BY day DESC
        """, taskId);

    // ---------- recurrence ----------

    public static Rule? RuleOf(TaskItem t) =>
        t.RecFreq != null && t.RecAnchor != null ? new(t.RecFreq, t.RecInterval, t.RecDays, t.RecAnchor) : null;

    /// <summary>Sets or removes (null) the rule of a task, and places today's occurrence if it is due.</summary>
    public static void SetRecurrence(Db db, int id, Rule? rule, string day)
    {
        db.Execute(
            "UPDATE tasks SET rec_freq = $2, rec_interval = $3, rec_days = $4, rec_anchor = $5, rec_last = NULL, rec_paused = 0 WHERE id = $1",
            id, rule?.Freq, rule?.Interval ?? 1, rule?.Days ?? 0, rule?.Anchor);
        if (rule != null) SpawnOccurrences(db, day);
    }

    public static void SetRoutinePaused(Db db, int id, bool paused, string day)
    {
        // Resuming does not bring back the occurrences missed while paused.
        db.Execute(
            "UPDATE tasks SET rec_paused = $2, rec_last = CASE WHEN $2 = 0 THEN $3 ELSE rec_last END WHERE id = $1",
            id, paused ? 1 : 0, Day.AddDays(day, -1));
        if (!paused) SpawnOccurrences(db, day);
    }

    /// <summary>
    /// Places the occurrence of every routine that is due on day.
    /// No stacking: a routine whose previous occurrence is still open gets nothing new.
    /// A finished one is reopened, with its sub-tasks, and enters Today.
    /// </summary>
    public static void SpawnOccurrences(Db db, string day)
    {
        var routines = db.Select<TaskItem>(
            "SELECT * FROM tasks WHERE rec_freq IS NOT NULL AND rec_paused = 0 AND parent_id IS NULL AND (rec_last IS NULL OR rec_last < $1)",
            day);
        foreach (var t in routines)
        {
            if (!Recurrence.IsDue(RuleOf(t)!, day)) continue;
            if (t.DoneAt != null)
            {
                db.Execute("UPDATE tasks SET done_at = NULL WHERE id = $1 OR parent_id = $1", t.Id);
                db.Execute("UPDATE tasks SET postpone_count = 0 WHERE id = $1", t.Id);
            }
            db.Execute(
                """
                UPDATE tasks SET rec_last = $2, today_date = COALESCE(today_date, $2),
                  today_position = CASE WHEN today_date IS NULL THEN (SELECT COALESCE(MAX(today_position), 0) + 1 FROM tasks) ELSE today_position END
                WHERE id = $1
                """, t.Id, day);
        }
    }

    /// <summary>Every task that has a rule, active ones first.</summary>
    public static List<TaskItem> Routines(Db db) =>
        Nest(db.Select<TaskItem>($"{TaskSelect} WHERE t.rec_freq IS NOT NULL ORDER BY t.rec_paused, t.title COLLATE NOCASE"));

    // ---------- reports ----------

    /// <summary>Per-day totals in a period. Days with nothing are absent.</summary>
    public static List<DayStats> DayStats(Db db, string from, string to) => db.Select<DayStats>(
        """
        SELECT day, SUM(done) AS done, SUM(postponed) AS postponed, SUM(completed) AS completed, SUM(interrupted) AS interrupted FROM (
          SELECT day, (outcome = 'done') AS done, (outcome = 'postponed') AS postponed, 0 AS completed, 0 AS interrupted FROM day_snapshots
          UNION ALL
          SELECT day, 0, 0, (status = 'completed'), (status = 'interrupted') FROM pomodoros
        ) WHERE day BETWEEN $1 AND $2 GROUP BY day ORDER BY day
        """, from, to);

    /// <summary>Focus time per project; project is null for tasks in loose lists or in no list.</summary>
    public static List<ProjectFocus> FocusByProject(Db db, string from, string to) => db.Select<ProjectFocus>(
        """
        SELECT project_name AS project, SUM(seconds) AS seconds FROM pomodoros
        WHERE day BETWEEN $1 AND $2 GROUP BY project_name ORDER BY seconds DESC
        """, from, to);

    /// <summary>Completed pomodoros per day, for the heatmap and the streaks.</summary>
    public static List<FocusDay> FocusDays(Db db, string from = "0000-00-00") => db.Select<FocusDay>(
        "SELECT day, COUNT(*) AS count FROM pomodoros WHERE status = 'completed' AND day >= $1 GROUP BY day ORDER BY day", from);

    public static List<Postponed> MostPostponed(Db db, string from, string to, int limit = 5) => db.Select<Postponed>(
        """
        SELECT COALESCE(t.title, s.task_title) AS title, s.project_name AS project, s.list_name AS list,
               COUNT(*) AS times, t.done_at AS doneAt, (t.id IS NOT NULL) AS "exists"
        FROM day_snapshots s LEFT JOIN tasks t ON t.id = s.task_id
        WHERE s.outcome = 'postponed' AND s.day BETWEEN $1 AND $2
        -- A deleted task has no id left; its rows are grouped by the copied title and project.
        GROUP BY s.task_id, CASE WHEN s.task_id IS NULL THEN s.task_title || char(31) || COALESCE(s.project_name, '') END
        ORDER BY times DESC, title LIMIT $3
        """, from, to, limit);

    /// <summary>The first closed day, or null before any day was closed.</summary>
    public static string? FirstClosedDay(Db db) => db.Select<string?>("SELECT MIN(day) FROM days")[0];
}
