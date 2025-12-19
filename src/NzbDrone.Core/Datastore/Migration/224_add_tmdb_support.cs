using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(224)]
    public class add_tmdb_support : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Per-series TMDB preference
            Alter.Table("Series").AddColumn("PreferTmdb").AsBoolean().WithDefaultValue(false);

            // Episode TMDB ID for matching
            Alter.Table("Episodes").AddColumn("TmdbId").AsInt32().WithDefaultValue(0);
            Create.Index().OnTable("Episodes").OnColumn("TmdbId");
        }
    }
}
