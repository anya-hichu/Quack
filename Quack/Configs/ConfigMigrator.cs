using Dalamud.Plugin;
using Quack.Macros;
using Quack.Schedulers;
using SQLite;
using System.IO;
using System.Linq;

namespace Quack.Configs;

public class ConfigMigrator(Config config, SQLiteConnection dbConnection, MacroTable macroTable)
{
    private Config Config { get; init; } = config;
    private SQLiteConnection DbConnection { get; init; } = dbConnection;
    private MacroTable MacroTable { get; init; } = macroTable;

    public void ExecuteMigrations()
    {
        var version = Config.Version;
        if (version < Config.CURRENT_VERSION)
        {
            if (version < 1)
            {
                Config.GeneratorConfigs.ForEach(c =>
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
                Config.ExtraCommandFormat = Config.CommandFormat;
                Config.CommandFormat = string.Empty;
            }

            if (version < 3)
            {
                MacroTable.Insert(Config.Macros);
                Config.Macros.Clear();
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
                Config.SchedulerConfigs.ForEach(MigrateSchedulerConfigToV6);
            }

            Config.Version = Config.CURRENT_VERSION;
            Config.Save();
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

    public static void MigrateDatabasePathToV6(IDalamudPluginInterface pluginInterface)
    {
        // Fix location since Loc means localization...
        var databaseFileName = $"{pluginInterface.InternalName}.db";
        var oldDatabasePath = Path.Combine(pluginInterface.GetPluginLocDirectory(), databaseFileName);
        if (File.Exists(oldDatabasePath))
        {
            File.Move(oldDatabasePath, Path.Combine(pluginInterface.GetPluginConfigDirectory(), databaseFileName));
        }
    }
}
