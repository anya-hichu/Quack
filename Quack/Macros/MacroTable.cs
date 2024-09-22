using Dalamud.Plugin.Services;
using Dalamud.Utility;
using SQLite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;

namespace Quack.Macros;

public class MacroTable(SQLiteConnection connection, IPluginLog pluginLog)
{
    private static readonly string[] COLUMNS = ["name", "path", "command", "tags", "content", "loop"];

    private static readonly string ALL = string.Join(", ", COLUMNS);
    private static readonly string VALUES = string.Join(", ", COLUMNS.Select(c => "?"));
    private static readonly string ALL_ASSIGNMENTS = string.Join(", ", COLUMNS.Select(c => $"{c}=?"));

    private static readonly string CREATE_TABLE_QUERY = "CREATE VIRTUAL TABLE IF NOT EXISTS macros USING fts5(name, path, command, tags, content UNINDEXED, loop UNINDEXED, tokenize='trigram');";
    private static readonly string LIST_QUERY = $"SELECT {ALL} FROM macros;";
    private static readonly string FIND_BY_QUERY = $"SELECT {ALL} FROM macros WHERE {{0}}=? LIMIT 1;";
    private static readonly string SEARCH_QUERY = $"SELECT {ALL} FROM macros WHERE macros MATCH ? ORDER BY rank;";
    private static readonly string INSERT_QUERY = $"INSERT INTO macros ({ALL}) VALUES ({VALUES})";
    private static readonly string UPDATE_QUERY = $"UPDATE macros SET {ALL_ASSIGNMENTS} WHERE path=?;";
    private static readonly string DELETE_EQ_QUERY = "DELETE FROM macros WHERE path=?;";
    private static readonly string DELETE_QUERY = "DELETE FROM macros;";

    // Max variables = 999 (https://www.sqlite.org/limits.html)
    private static readonly int INSERT_CHUNK_SIZE = 100;
    private static readonly int DELETE_CHUNK_SIZE = 500;

    private SQLiteConnection Connection { get; init; } = connection;
    private IPluginLog PluginLog { get; init; } = pluginLog;

    public event Action? OnChange;

    public void MaybeCreateTable()
    {
        var result = Connection.Execute(CREATE_TABLE_QUERY);
        LogQuery(CREATE_TABLE_QUERY, result);
    }

    public HashSet<Macro> List()
    {
        var records = Connection.Query<MacroRecord>(LIST_QUERY);
        LogQuery(LIST_QUERY, records.Count);
        return toEntities(records);
    }

    public Macro? FindBy(string column, string value)
    {
        object[] args = [value];
        var query = FIND_BY_QUERY.Format(column);
        var records = Connection.Query<MacroRecord>(query, args);
        LogQuery(query, records.Count, args);
        return records.Count > 0 ? ToEntity(records.First()) : null;
    }

    public HashSet<Macro> Search(string expression)
    {
        object[] args = [expression];
        var records = Connection.Query<MacroRecord>(SEARCH_QUERY, args);
        LogQuery(SEARCH_QUERY, records.Count, args);
        return toEntities(records);
    }

    public int Insert(Macro macro)
    {
        var args = ToValues(macro);
        var result = Connection.Execute(INSERT_QUERY, args);
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
                var chunkResult = Connection.Execute(query, args);
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

    public int Update(Macro macro)
    {
        return Update(macro.Path, macro);
    }

    public int Update(string currentPath, Macro macro)
    {
        object[] args = [..ToValues(macro), currentPath];
        var result = Connection.Execute(UPDATE_QUERY, args);
        LogQuery(UPDATE_QUERY, result, args);
        MaybeNotifyChange(result);
        return result;
    }

    public int Delete(Macro macro)
    {
        object[] args = [macro.Path];
        var result = Connection.Execute(DELETE_EQ_QUERY, args);
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
                var chunkResult = Connection.Execute(query, args);
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
        var result = Connection.Execute(DELETE_QUERY);
        LogQuery(DELETE_QUERY, result);
        MaybeNotifyChange(result);
        return result;
    }

    private void MaybeNotifyChange(int result)
    {
        if (result > 0)
        {
            OnChange?.Invoke();
            PluginLog.Debug("Notified macros changed");
        }
    }

    private void LogQuery(string query, int result)
    {
        PluginLog.Debug($"Executed query '{query}' with result {result}");
    }

    private void LogQuery(string query, int result, object[] args)
    {
        PluginLog.Debug($"Executed query '{query}' {JsonSerializer.Serialize(args)} with result {result}");
    }

    private static object[] ToValues(Macro macro)
    {
        return [
            macro.Name,
            macro.Path,
            macro.Command,
            string.Join(',', macro.Tags),
            macro.Content,
            macro.Loop.ToString()
        ];
    }

    private static HashSet<Macro> toEntities(IEnumerable<MacroRecord> records)
    {
        return new(records.Select(ToEntity), MacroComparer.INSTANCE);
    }

    private static Macro ToEntity(MacroRecord macroRecord)
    {
        var macro = new Macro();
        macro.Name = macroRecord.Name!;
        macro.Path = macroRecord.Path!;
        macro.Command = macroRecord.Command!;
        macro.Tags = macroRecord.Tags!.Split(',');
        macro.Content = macroRecord.Content!;
        macro.Loop = bool.Parse(macroRecord.Loop!);
        return macro;
    }
}
