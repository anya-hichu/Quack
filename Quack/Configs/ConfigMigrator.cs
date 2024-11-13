using Quack.Macros;
using Quack.Schedulers;
using SQLite;
using System.Linq;

namespace Quack.Configs;

public class ConfigMigrator(SQLiteConnection dbConnection, MacroTable macroTable)
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
                    c.IpcConfigs.Add(new()
                    {
                        Name = c.IpcName,
                        Args = c.IpcArgs
                    });
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

            if (version < 6)
            {
                config.SchedulerConfigs.ForEach(MigrateSchedulerConfigToV6);
            }

            config.Version = Config.CURRENT_VERSION;
            config.Save();
        }
    }

    public static void MigrateSchedulerConfigToV6(SchedulerConfig schedulerConfig)
    {
        var schedulerCommandConfigs = schedulerConfig.SchedulerCommandConfigs;
        if (schedulerCommandConfigs.Count > 0)
        {
            // Optimized V5 migration
            schedulerConfig.TriggerConfigs = new(schedulerCommandConfigs.Select(c => new SchedulerTriggerConfig()
            {
                TimeZone = c.TimeZone,
                TimeExpression = c.TimeExpression,
                Command = c.Command
            }));
            schedulerCommandConfigs.Clear();
        } 
        else
        {
            var schedulerTriggerConfigs = schedulerConfig.SchedulerTriggerConfigs;
            if (schedulerTriggerConfigs.Count > 0)
            {
                schedulerConfig.TriggerConfigs = schedulerTriggerConfigs;
                schedulerTriggerConfigs.Clear();
            }
        }
    }
}
