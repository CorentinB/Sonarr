using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.Aggregators.Augmenters.Quality;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Aggregation.Aggregators.Augmenters.Quality
{
    [TestFixture]
    public class AugmentQualityFromReleaseNameFixture : CoreTest<AugmentQualityFromReleaseName>
    {
        private LocalEpisode _localEpisode;
        private DownloadClientItem _downloadClientItem;
        private ParsedEpisodeInfo _hdtvParsedEpisodeInfo;
        private ParsedEpisodeInfo _webdlParsedEpisodeInfo;

        [SetUp]
        public void Setup()
        {
            _hdtvParsedEpisodeInfo = Builder<ParsedEpisodeInfo>.CreateNew()
                                                               .With(p => p.Quality =
                                                                   new QualityModel(Core.Qualities.Quality.HDTV720p))
                                                               .Build();

            _webdlParsedEpisodeInfo = Builder<ParsedEpisodeInfo>.CreateNew()
                                                                .With(p => p.Quality =
                                                                    new QualityModel(Core.Qualities.Quality.WEBDL720p))
                                                                .Build();

            _localEpisode = Builder<LocalEpisode>.CreateNew()
                                                 .With(l => l.FolderEpisodeInfo = _hdtvParsedEpisodeInfo)
                                                 .With(l => l.FileEpisodeInfo = _hdtvParsedEpisodeInfo)
                                                 .Build();

            _downloadClientItem = Builder<DownloadClientItem>.CreateNew()
                                                             .Build();
        }

        [Test]
        public void should_return_null_if_download_client_item_is_null()
        {
            Subject.AugmentQuality(_localEpisode, null).Should().BeNull();
        }

        [Test]
        public void should_return_null_if_folder_quality_source_is_not_hdtv()
        {
            _localEpisode.FolderEpisodeInfo = _webdlParsedEpisodeInfo;
            _localEpisode.FileEpisodeInfo = _hdtvParsedEpisodeInfo;

            Subject.AugmentQuality(_localEpisode, _downloadClientItem).Should().BeNull();
        }
        
        [Test]
        public void should_return_null_if_file_quality_source_is_not_hdtv()
        {
            _localEpisode.FolderEpisodeInfo = null;
            _localEpisode.FileEpisodeInfo = _webdlParsedEpisodeInfo;

            Subject.AugmentQuality(_localEpisode, _downloadClientItem).Should().BeNull();
        }

        [Test]
        public void should_return_null_if_no_grabbed_history()
        {
            _localEpisode.FileEpisodeInfo = _hdtvParsedEpisodeInfo;

            Mocker.GetMock<IDownloadHistoryService>()
                  .Setup(s => s.GetLatestGrab(It.IsAny<string>()))
                  .Returns((DownloadHistory)null);

            Subject.AugmentQuality(_localEpisode, _downloadClientItem).Should().BeNull();
        }

        [Test]
        public void should_not_return_augmented_quality_if_local_quality_source_is_name()
        {
            _localEpisode.FolderEpisodeInfo.Quality.SourceDetectionSource = QualityDetectionSource.Name;
            _localEpisode.FolderEpisodeInfo.Quality.ResolutionDetectionSource = QualityDetectionSource.Name;
            _localEpisode.FolderEpisodeInfo = _hdtvParsedEpisodeInfo;

            Mocker.GetMock<IDownloadHistoryService>()
                  .Setup(s => s.GetLatestGrab(It.IsAny<string>()))
                  .Returns(Builder<DownloadHistory>.CreateNew()
                                                   .With(h => h.SourceTitle = "Series.Title.S01E01.720p.WEB.x264")
                                                   .Build()
                  );

            Subject.AugmentQuality(_localEpisode, _downloadClientItem).Should().BeNull();
        }

        [Test]
        public void should_not_return_augmented_quality_if_local_quality_source_detection_source_is_name()
        {
            _localEpisode.FolderEpisodeInfo.Quality.SourceDetectionSource = QualityDetectionSource.Name;
            _localEpisode.FolderEpisodeInfo.Quality.ResolutionDetectionSource = QualityDetectionSource.Name;
            _localEpisode.FolderEpisodeInfo = _hdtvParsedEpisodeInfo;

            Mocker.GetMock<IDownloadHistoryService>()
                  .Setup(s => s.GetLatestGrab(It.IsAny<string>()))
                  .Returns(Builder<DownloadHistory>.CreateNew()
                                                   .With(h => h.SourceTitle = "Series.Title.S01E01.720p.WEB.x264")
                                                   .Build()
                  );

            Subject.AugmentQuality(_localEpisode, _downloadClientItem).Should().BeNull();
        }

        [Test]
        public void should_return_augmented_quality_with_source_if_local_source_detection_source_is_not_name()
        {
            _localEpisode.FolderEpisodeInfo.Quality.SourceDetectionSource = QualityDetectionSource.Unknown;
            _localEpisode.FolderEpisodeInfo.Quality.ResolutionDetectionSource = QualityDetectionSource.Name;
            _localEpisode.FolderEpisodeInfo = _hdtvParsedEpisodeInfo;

            Mocker.GetMock<IDownloadHistoryService>()
                  .Setup(s => s.GetLatestGrab(It.IsAny<string>()))
                  .Returns(Builder<DownloadHistory>.CreateNew()
                                                   .With(h => h.SourceTitle = "Series.Title.S01E01.720p.WEB.x264")
                                                   .Build()
                           );

            var result = Subject.AugmentQuality(_localEpisode, _downloadClientItem);
            
            result.Should().NotBe(null);
            result.Source.Should().Be(QualitySource.Web);
            result.SourceConfidence.Should().Be(Confidence.Tag);
            result.Resolution.Should().Be(0);
            result.ResolutionConfidence.Should().Be(Confidence.Default);
        }

        [Test]
        public void should_return_augmented_quality_with_resolution_if_local_resolution_detection_source_is_not_name()
        {
            _localEpisode.FolderEpisodeInfo.Quality.SourceDetectionSource = QualityDetectionSource.Name;
            _localEpisode.FolderEpisodeInfo.Quality.ResolutionDetectionSource = QualityDetectionSource.Unknown;
            _localEpisode.FolderEpisodeInfo = _hdtvParsedEpisodeInfo;

            Mocker.GetMock<IDownloadHistoryService>()
                  .Setup(s => s.GetLatestGrab(It.IsAny<string>()))
                  .Returns(Builder<DownloadHistory>.CreateNew()
                                                   .With(h => h.SourceTitle = "Series.Title.S01E01.1080p.WEB.x264")
                                                   .Build()
                  );

            var result = Subject.AugmentQuality(_localEpisode, _downloadClientItem);

            result.Should().NotBe(null);
            result.Source.Should().Be(QualitySource.Unknown);
            result.SourceConfidence.Should().Be(Confidence.Default);
            result.Resolution.Should().Be(1080);
            result.ResolutionConfidence.Should().Be(Confidence.Tag);
        }

        [Test]
        public void should_return_full_augmented_quality_if_local_source_detection_sources_are_not_name()
        {
            _localEpisode.FolderEpisodeInfo.Quality.SourceDetectionSource = QualityDetectionSource.Unknown;
            _localEpisode.FolderEpisodeInfo.Quality.ResolutionDetectionSource = QualityDetectionSource.Unknown;
            _localEpisode.FolderEpisodeInfo = _hdtvParsedEpisodeInfo;

            Mocker.GetMock<IDownloadHistoryService>()
                  .Setup(s => s.GetLatestGrab(It.IsAny<string>()))
                  .Returns(Builder<DownloadHistory>.CreateNew()
                                                   .With(h => h.SourceTitle = "Series.Title.S01E01.1080p.WEB.x264")
                                                   .Build()
                  );

            var result = Subject.AugmentQuality(_localEpisode, _downloadClientItem);

            result.Should().NotBe(null);
            result.Source.Should().Be(QualitySource.Web);
            result.SourceConfidence.Should().Be(Confidence.Tag);
            result.Resolution.Should().Be(1080);
            result.ResolutionConfidence.Should().Be(Confidence.Tag);
        }
    }
}
