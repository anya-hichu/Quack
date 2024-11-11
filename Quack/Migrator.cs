using Quack.Macros;
using Quack.Schedulers;
using SQLite;
using System.Linq;

namespace Quack;

public class Migrator(SQLiteConnection dbConnection, MacroTable macroTable)
{
    private SQLiteConnection DbConnection { get; init; } = dbConnection;
    private MacroTable MacroTable { get; init; } = macroTable;

    public void ExecuteMigrations(Config config)
    {
        var version = config.Version;

        if (version < Config.CURRENT_VERSION)
        {
            if (version < 1)
            {
                config.GeneratorConfigs.ForEach(c =>
                {
                    c.IpcConfigs.Add(new(c.IpcName, c.IpcArgs));
                    c.IpcName = c.IpcArgs = string.Empty;
                });
            }

            if (version < 2)
            {
                config.ExtraCommandFormat = config.CommandFormat;
                config.CommandFormat = string.Empty;
            }

            if (version < 3)
            {
                MacroTable.Insert(config.Macros);
                config.Macros.Clear();
            }

            if (version < 4)
            {
                var macroRecords = DbConnection.Query<MacroRecord>("SELECT * FROM macros;");
                var macros = macroRecords.Select(record =>
                {
                    record.Args = string.Empty;
                    return MacroTable.ToEntity(record);
                });
                MacroTable.RecreateTable();
                MacroTable.Insert(macros);
            }

            if (version < 5)
            {
                config.SchedulerConfigs.ForEach(MigrateSchedulerConfigToV5);
            }

            config.Version = Config.CURRENT_VERSION;
            config.Save();
        }
    }

    public static void MigrateSchedulerConfigToV5(SchedulerConfig schedulerConfig)
    {
        schedulerConfig.SchedulerTriggerConfigs = schedulerConfig.SchedulerCommandConfigs.Select(c => new SchedulerTriggerConfig()
        {
            TimeZone = c.TimeZone,
            TimeExpression = c.TimeExpression,
            Command = c.Command
        }).ToList();
        schedulerConfig.SchedulerCommandConfigs.Clear();
    }
}
