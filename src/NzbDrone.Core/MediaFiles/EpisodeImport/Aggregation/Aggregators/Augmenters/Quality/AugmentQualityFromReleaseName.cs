using System.Linq;
using NzbDrone.Core.Download;
using NzbDrone.Core.History;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.Aggregators.Augmenters.Quality
{
    public class AugmentQualityFromReleaseName : IAugmentQuality
    {
        private readonly IHistoryService _historyService;

        public AugmentQualityFromReleaseName(IHistoryService historyService)
        {
            _historyService = historyService;
        }

        public AugmentQualityResult AugmentQuality(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            // Don't try to augment if we can't lookup the grabbed history by downloadId
            if (downloadClientItem == null)
            {
                return null;
            }

            var fileQuality = localEpisode.FileEpisodeInfo?.Quality.Quality;
            var folderQuality = localEpisode.FolderEpisodeInfo?.Quality.Quality;
            var localQuality = folderQuality ?? fileQuality;

            // Return early if the file or folder quality is not a television source (preferring the folder over the file)
            if (localQuality?.Source != QualitySource.Television)
            {
                return null;
            }

            var history = _historyService.FindByDownloadId(downloadClientItem.DownloadId)
                                         .OrderByDescending(h => h.Date)
                                         .FirstOrDefault(h => h.EventType == EpisodeHistoryEventType.Grabbed);

            if (history == null)
            {
                return null;
            }

            var historyQuality = history.Quality.Quality;

            // If the quality source is television (which will be the used if the file/folder name doesn't indicate a source) and the
            // resolution of the file or folder matches the resolution from the indexer then augment using the source from the indexer.

            if (localQuality.Resolution == historyQuality.Resolution)
            {
                return AugmentQualityResult.SourceOnly(historyQuality.Source, Confidence.Tag);
            }

            return null;
        }
    }
}
