using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.Aggregators.Augmenters.Quality
{
    public class AugmentQualityFromReleaseName : IAugmentQuality
    {
        private readonly IDownloadHistoryService _downloadHistoryService;

        public AugmentQualityFromReleaseName(IDownloadHistoryService downloadHistoryService)
        {
            _downloadHistoryService = downloadHistoryService;
        }

        public AugmentQualityResult AugmentQuality(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            // Don't try to augment if we can't lookup the grabbed history by downloadId
            if (downloadClientItem == null)
            {
                return null;
            }

            var fileQuality = localEpisode.FileEpisodeInfo?.Quality;
            var folderQuality = localEpisode.FolderEpisodeInfo?.Quality;
            var localQuality = folderQuality ?? fileQuality;

            // Return early if the file or folder quality source was parsed.
            if (localQuality?.SourceDetectionSource != QualityDetectionSource.Unknown)
            {
                return null;
            }

            var history = _downloadHistoryService.GetLatestGrab(downloadClientItem.DownloadId);

            if (history == null)
            {
                return null;
            }

            var historyQuality = QualityParser.ParseQuality(history.SourceTitle);

            // If the resolution was parsed from the name instead of unknown or the extension and it matches the grab history use the source from the grabbed history.

            if (localQuality.SourceDetectionSource != QualityDetectionSource.Name &&
                localQuality.ResolutionDetectionSource == QualityDetectionSource.Name &&
                historyQuality.ResolutionDetectionSource == QualityDetectionSource.Name &&
                localQuality.Quality.Resolution == historyQuality.Quality.Resolution)
            {
                return AugmentQualityResult.SourceOnly(historyQuality.Quality.Source, Confidence.Tag);
            }

            return null;
        }
    }
}
