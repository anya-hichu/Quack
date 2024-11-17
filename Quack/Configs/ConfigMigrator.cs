using Dalamud.Plugin;
using Quack.Macros;
using Quack.Schedulers;
using SQLite;
using System.IO;
using System.Linq;

namespace Quack.Configs;

public class ConfigMigrator(SQLiteConnection dbConnection, MacroTable macroTable)
{
    private SQLiteConnection DbConnection { get; init; } = dbConnection;
    private MacroTable MacroTable { get; init; } = macroTable;

    public void MaybeMigrate(Config config)
    {
        var version = config.Version;
        var currentVersion = Config.CURRENT_VERSION;

        if (version < currentVersion)
        {
            if (version < 1)
            {
                config.GeneratorConfigs.ForEach(generatorConfig =>
                {
                    generatorConfig.IpcConfigs.Add(new()
                    {
                        Name = generatorConfig.IpcName,
                        Args = generatorConfig.IpcArgs
                    });
                    generatorConfig.IpcName = generatorConfig.IpcArgs = string.Empty;
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
                config.SchedulerConfigs.ForEach(MigrateToV6);
            }

            config.Version = currentVersion;
            config.Save();
        }
    }

    public static void MigrateToV6(SchedulerConfig schedulerConfig)
    {
        var schedulerCommandConfigs = schedulerConfig.SchedulerCommandConfigs;
        if (schedulerCommandConfigs.Count > 0)
        {
            // Optimized V5 migration
            schedulerConfig.TriggerConfigs = new(schedulerCommandConfigs.Select(generatorConfig => new SchedulerTriggerConfig()
            {
                TimeZone = generatorConfig.TimeZone,
                TimeExpression = generatorConfig.TimeExpression,
                Command = generatorConfig.Command
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
