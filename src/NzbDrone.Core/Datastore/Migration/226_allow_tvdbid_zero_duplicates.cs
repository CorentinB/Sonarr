using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(226)]
    public class allow_tvdbid_zero_duplicates : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Drop the existing unique index on TvdbId
            Delete.Index("IX_Series_TvdbId").OnTable("Series");

            // Create a partial unique index that only applies when TvdbId > 0
            // This allows multiple TMDB-only series (TvdbId = 0) while preventing
            // duplicate TVDB IDs for series that have them
            Execute.Sql("CREATE UNIQUE INDEX \"IX_Series_TvdbId\" ON \"Series\" (\"TvdbId\") WHERE \"TvdbId\" > 0");
        }
    }
}
