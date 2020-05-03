using System;
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

            if (localQuality == null)
            {
                return null;
            }

            // Return early if the file or folder quality source was parsed from the name.
            if (localQuality.SourceDetectionSource == QualityDetectionSource.Name &&
                localQuality.ResolutionDetectionSource == QualityDetectionSource.Name)
            {
                return null;
            }

            var history = _downloadHistoryService.GetLatestGrab(downloadClientItem.DownloadId);

            if (history == null)
            {
                return null;
            }

            var historyQuality = QualityParser.ParseQuality(history.SourceTitle);

            // Only return a source and/or resolution if the release name parsing matched via the name and the
            // local folder/file did not. This way we'll override filenames that don't have the proper source/resolution
            // with the source/resolution from the release name, but not attempt to override those that do.

            var source = localQuality.SourceDetectionSource != QualityDetectionSource.Name &&
                         historyQuality.SourceDetectionSource == QualityDetectionSource.Name
                ? historyQuality.Quality.Source
                : QualitySource.Unknown;

            var resolution = localQuality.ResolutionDetectionSource != QualityDetectionSource.Name &&
                              historyQuality.ResolutionDetectionSource == QualityDetectionSource.Name
                ? historyQuality.Quality.Resolution
                : (int?)null;

            var revision = historyQuality.SourceDetectionSource == QualityDetectionSource.Name &&
                           historyQuality.ResolutionDetectionSource == QualityDetectionSource.Name
                ? historyQuality.Revision
                : null;

            if (source != QualitySource.Unknown && resolution.HasValue)
            {
                return new AugmentQualityResult(source, Confidence.Tag, resolution.Value, Confidence.Tag, revision);
            }

            if (source != QualitySource.Unknown && !resolution.HasValue)
            {
                return AugmentQualityResult.SourceOnly(source, Confidence.Tag);
            }

            if (source == QualitySource.Unknown && resolution.HasValue)
            {
                return AugmentQualityResult.ResolutionOnly(resolution.Value, Confidence.Tag);
            }

            return null;
        }
    }
}
