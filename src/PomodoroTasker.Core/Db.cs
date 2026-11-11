using System.Reflection;
using Microsoft.Data.Sqlite;

namespace PomodoroTasker.Core;

/// <summary>The storage seam: one SQLite connection, positional parameters named $1..$n.</summary>
public sealed class Db : IDisposable
{
    private readonly SqliteConnection _connection;

    public Db(string dataSource)
    {
        _connection = new SqliteConnection($"Data Source={dataSource}");
        _connection.Open();
        Execute("PRAGMA foreign_keys = ON"); // the cascades depend on it
        Migrate();
    }

    /// <summary>
    /// Rows as T. A scalar T reads the first column; a class is filled by column name, where
    /// snake_case and camelCase columns both match a PascalCase property.
    /// </summary>
    public List<T> Select<T>(string sql, params object?[] args)
    {
        using var command = Command(sql, args);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        var type = typeof(T);
        var scalar = type == typeof(string) || (Nullable.GetUnderlyingType(type) ?? type).IsPrimitive;
        while (reader.Read())
        {
            if (scalar)
            {
                rows.Add((T)Convert(reader.GetValue(0), type)!);
                continue;
            }
            var row = Activator.CreateInstance<T>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var property = type.GetProperty(reader.GetName(i).Replace("_", ""),
                    BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                property?.SetValue(row, Convert(reader.GetValue(i), property.PropertyType));
            }
            rows.Add(row);
        }
        return rows;
    }

    private static object? Convert(object value, Type type) =>
        value is DBNull ? null : System.Convert.ChangeType(value, Nullable.GetUnderlyingType(type) ?? type);

    /// <summary>Runs one statement and answers the last inserted row id.</summary>
    public int Execute(string sql, params object?[] args)
    {
        using var command = Command(sql, args);
        command.ExecuteNonQuery();
        using var last = Command("SELECT last_insert_rowid()", []);
        return (int)(long)last.ExecuteScalar()!;
    }

    public SqliteTransaction Begin() => _connection.BeginTransaction();

    public void Dispose() => _connection.Dispose();

    private SqliteCommand Command(string sql, object?[] args)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < args.Length; i++)
            command.Parameters.AddWithValue($"${i + 1}", args[i] ?? DBNull.Value);
        return command;
    }

    private void Migrate()
    {
        var version = Select<int>("PRAGMA user_version")[0];
        // A database the desktop app created was migrated by sqlx, which keeps its own table.
        if (version == 0 && Select<int>("SELECT 1 FROM sqlite_master WHERE name = '_sqlx_migrations'").Count > 0)
            version = Select<int>("SELECT COUNT(*) FROM _sqlx_migrations")[0];

        var assembly = typeof(Db).Assembly;
        var files = assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql")).Order().ToList();
        for (var i = version; i < files.Count; i++)
        {
            using var stream = new StreamReader(assembly.GetManifestResourceStream(files[i])!);
            using var transaction = Begin();
            Execute(stream.ReadToEnd());
            Execute($"PRAGMA user_version = {i + 1}");
            transaction.Commit();
        }
    }
}
