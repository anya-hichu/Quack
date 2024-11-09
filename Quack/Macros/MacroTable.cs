using Dalamud.Plugin.Services;
using Dalamud.Utility;
using SQLite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;

namespace Quack.Macros;

public class MacroTable(SQLiteConnection dbConnection, IPluginLog pluginLog)
{
    private static readonly string[] COLUMNS = ["name", "path", "tags", "command", "args", "content", "loop"];

    private static readonly string ALL = string.Join(", ", COLUMNS);
    private static readonly string VALUES = string.Join(", ", COLUMNS.Select(c => "?"));

    private static readonly string CREATE_TABLE_QUERY = "CREATE VIRTUAL TABLE IF NOT EXISTS macros USING fts5(name, path, command, args UNINDEXED, tags, content UNINDEXED, loop UNINDEXED, tokenize='trigram');";
    private static readonly string DROP_TABLE_QUERY = "DROP TABLE IF EXISTS macros";
    
    private static readonly string LIST_QUERY = $"SELECT {ALL} FROM macros;";
    private static readonly string FIND_BY_QUERY = $"SELECT {ALL} FROM macros WHERE {{0}}=? LIMIT 1;";
    private static readonly string SEARCH_QUERY = $"SELECT {ALL} FROM macros WHERE macros MATCH ? ORDER BY rank;";
    private static readonly string INSERT_QUERY = $"INSERT INTO macros ({ALL}) VALUES ({VALUES})";
    private static readonly string UPDATE_QUERY = $"UPDATE macros SET {{0}}=? WHERE path=?;";
    private static readonly string DELETE_EQ_QUERY = "DELETE FROM macros WHERE path=?;";
    private static readonly string DELETE_QUERY = "DELETE FROM macros;";

    // Max variables = 999 (https://www.sqlite.org/limits.html)
    private static readonly int INSERT_CHUNK_SIZE = 100;
    private static readonly int DELETE_CHUNK_SIZE = 500;

    private SQLiteConnection DbConnection { get; init; } = dbConnection;
    private IPluginLog PluginLog { get; init; } = pluginLog;

    public event Action? OnChange;

    public void MaybeCreateTable()
    {
        var result = DbConnection.Execute(CREATE_TABLE_QUERY);
        LogQuery(CREATE_TABLE_QUERY, result);
    }

    public void MaybeDropTable()
    {
        var result = DbConnection.Execute(DROP_TABLE_QUERY);
        LogQuery(DROP_TABLE_QUERY, result);
    }

    public void RecreateTable()
    {
        // Virtual tables do not support ALTER TABLE so recreating it is required
        MaybeDropTable();
        MaybeCreateTable();
    }

    public HashSet<Macro> List()
    {
        var records = DbConnection.Query<MacroRecord>(LIST_QUERY);
        LogQuery(LIST_QUERY, records.Count);
        return toEntities(records);
    }

    public Macro? FindBy(string column, string value)
    {
        object[] args = [value];
        var query = FIND_BY_QUERY.Format(column);
        var records = DbConnection.Query<MacroRecord>(query, args);
        LogQuery(query, records.Count, args);
        return records.Count > 0 ? ToEntity(records.First()) : null;
    }

    public HashSet<Macro> Search(string expression)
    {
        object[] args = [expression];
        var records = DbConnection.Query<MacroRecord>(SEARCH_QUERY, args);
        LogQuery(SEARCH_QUERY, records.Count, args);
        return toEntities(records);
    }

    public int Insert(Macro macro)
    {
        var args = ToValues(macro);
        var result = DbConnection.Execute(INSERT_QUERY, args);
        LogQuery(INSERT_QUERY, result, args);
        MaybeNotifyChange(result);
        return result;
    }

    public int Insert(IEnumerable<Macro> macros)
    {
        if (macros.Any())
        {
            var result = 0;
            foreach (var chunk in macros.Chunk(INSERT_CHUNK_SIZE))
            {
                var query = $"INSERT INTO macros ({ALL}) VALUES {string.Join(", ", chunk.Select(m => $"({VALUES})"))}";
                var args = chunk.SelectMany(ToValues).ToArray();
                var chunkResult = DbConnection.Execute(query, args);
                LogQuery(query, chunkResult, args);
                result += chunkResult;
            }

            MaybeNotifyChange(result);
            return result;
        }
        else
        {
            return 0;
        }
    }

    public int Update(string column, Macro macro)
    {
        return Update(column, macro, macro.Path);
    }

    public int Update(string column, Macro macro, string path)
    {
        var value = ToValues(macro).ElementAt(COLUMNS.IndexOf(column));
        object[] args = [value, path];
        var query = UPDATE_QUERY.Format(column);
        var result = DbConnection.Execute(query, args);
        LogQuery(query, result, args);
        MaybeNotifyChange(result);
        return result;
    }

    public int Delete(Macro macro)
    {
        object[] args = [macro.Path];
        var result = DbConnection.Execute(DELETE_EQ_QUERY, args);
        LogQuery(DELETE_EQ_QUERY, result, args);
        MaybeNotifyChange(result);
        return result;
    }

    public int Delete(IEnumerable<Macro> macros)
    {
        if (macros.Any())
        {
            var result = 0;
            foreach (var chunk in macros.Chunk(DELETE_CHUNK_SIZE))
            {
                var query = $"DELETE FROM macros WHERE path IN ({string.Join(", ", chunk.Select(m => "?"))});";
                object[] args = chunk.Select(m => m.Path).ToArray();
                var chunkResult = DbConnection.Execute(query, args);
                LogQuery(query, chunkResult, args);
                result += chunkResult;
            }
            MaybeNotifyChange(result);
            return result;
        }
        else
        {
            return 0;
        }
    }

    public int DeleteAll()
    {
        var result = DbConnection.Execute(DELETE_QUERY);
        LogQuery(DELETE_QUERY, result);
        MaybeNotifyChange(result);
        return result;
    }

    private void MaybeNotifyChange(int result)
    {
        if (result > 0)
        {
            OnChange?.Invoke();
            PluginLog.Verbose("Notified macros changed");
        }
    }

    private void LogQuery(string query, int result)
    {
        PluginLog.Verbose($"Executed query '{query}' with result {result}");
    }

    private void LogQuery(string query, int result, object[] args)
    {
        PluginLog.Verbose($"Executed query '{query}' {JsonSerializer.Serialize(args)} with result {result}");
    }

    private static object[] ToValues(Macro macro)
    {
        return [
            macro.Name,
            macro.Path,
            string.Join(',', macro.Tags),
            macro.Command,
            macro.Args,
            macro.Content,
            macro.Loop.ToString()
        ];
    }

    public static HashSet<Macro> toEntities(IEnumerable<MacroRecord> records)
    {
        return new(records.Select(ToEntity), MacroComparer.INSTANCE);
    }

    public static Macro ToEntity(MacroRecord macroRecord)
    {
        return new()
        {
            Name = macroRecord.Name!,
            Path = macroRecord.Path!,
            Tags = macroRecord.Tags!.Split(','),
            Command = macroRecord.Command!,
            Args = macroRecord.Args!,
            Content = macroRecord.Content!,
            Loop = bool.Parse(macroRecord.Loop!)
        };
    }
}
