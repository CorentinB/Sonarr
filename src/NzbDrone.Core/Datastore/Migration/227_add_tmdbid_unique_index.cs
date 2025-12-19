using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(227)]
    public class add_tmdbid_unique_index : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Create a partial unique index on TmdbId that only applies when TmdbId > 0
            // This prevents duplicate TMDB-only series from being added
            // TmdbId = 0 means no TMDB ID, which is allowed to have duplicates
            Execute.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Series_TmdbId\" ON \"Series\" (\"TmdbId\") WHERE \"TmdbId\" > 0");
        }
    }
}
