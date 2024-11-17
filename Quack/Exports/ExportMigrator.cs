using Quack.Configs;
using Quack.Exports;
using Quack.Schedulers;

namespace Quack.Exports;

public static class ExportMigrator
{
    public static void MaybeMigrate(Export<SchedulerConfig> export)
    {
        var version = export.Version;
        var currentVersion = Config.CURRENT_VERSION;

        if (version < currentVersion)
        {
            if (version < 6)
            {
                export.Entities.ForEach(ConfigMigrator.MigrateToV6);
            }
            export.Version = currentVersion;
        }
    }
}
