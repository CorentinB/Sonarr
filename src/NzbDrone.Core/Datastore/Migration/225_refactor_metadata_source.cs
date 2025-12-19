using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(225)]
    public class refactor_metadata_source : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Add new MetadataSource column (0 = Tvdb, 1 = Tmdb)
            Alter.Table("Series").AddColumn("MetadataSource").AsInt32().WithDefaultValue(0);

            // Migrate data: PreferTmdb = true -> MetadataSource = 1 (Tmdb)
            Execute.Sql("UPDATE \"Series\" SET \"MetadataSource\" = 1 WHERE \"PreferTmdb\" = 1");

            // Remove old PreferTmdb column
            Delete.Column("PreferTmdb").FromTable("Series");
        }
    }
}
