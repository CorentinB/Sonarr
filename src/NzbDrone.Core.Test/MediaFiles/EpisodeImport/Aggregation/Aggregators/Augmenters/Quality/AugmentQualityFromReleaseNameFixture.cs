using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.History;
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
            Subject.AugmentQuality(_localEpisode, null).Should().Be(null);
        }

        [Test]
        public void should_return_null_if_folder_quality_source_is_not_hdtv()
        {
            _localEpisode.FolderEpisodeInfo = _webdlParsedEpisodeInfo;
            _localEpisode.FileEpisodeInfo = _hdtvParsedEpisodeInfo;

            Subject.AugmentQuality(_localEpisode, _downloadClientItem).Should().Be(null);
        }
        
        [Test]
        public void should_return_null_if_file_quality_source_is_not_hdtv()
        {
            _localEpisode.FolderEpisodeInfo = null;
            _localEpisode.FileEpisodeInfo = _webdlParsedEpisodeInfo;

            Subject.AugmentQuality(_localEpisode, _downloadClientItem).Should().Be(null);
        }

        [Test]
        public void should_return_null_if_no_history()
        {
            _localEpisode.FileEpisodeInfo = _hdtvParsedEpisodeInfo;

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<EpisodeHistory>());

            Subject.AugmentQuality(_localEpisode, _downloadClientItem).Should().Be(null);
        }

        [Test]
        public void should_return_null_if_no_grabbed_history()
        {
            _localEpisode.FileEpisodeInfo = _hdtvParsedEpisodeInfo;

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<EpisodeHistory>
                           {
                               Builder<EpisodeHistory>.CreateNew()
                                                      .With(h => h.EventType = EpisodeHistoryEventType.DownloadFolderImported)
                                                      .Build()
                           });

            Subject.AugmentQuality(_localEpisode, _downloadClientItem).Should().Be(null);
        }

        [Test]
        public void should_return_null_if_grabbed_history_resolution_does_not_match()
        {
            _localEpisode.FolderEpisodeInfo = _hdtvParsedEpisodeInfo;

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<EpisodeHistory>
                           {
                               Builder<EpisodeHistory>.CreateNew()
                                                      .With(h => h.EventType = EpisodeHistoryEventType.Grabbed)
                                                      .With(h => h.Quality = new QualityModel(Core.Qualities.Quality.WEBDL1080p))
                                                      .Build()
                           });

            Subject.AugmentQuality(_localEpisode, _downloadClientItem).Should().Be(null);
        }

        [Test]
        public void should_return_augmented_quality_if_grabbed_history_resolution_matches()
        {
            _localEpisode.FolderEpisodeInfo = _hdtvParsedEpisodeInfo;

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<EpisodeHistory>
                           {
                               Builder<EpisodeHistory>.CreateNew()
                                                      .With(h => h.EventType = EpisodeHistoryEventType.Grabbed)
                                                      .With(h => h.Quality = new QualityModel(Core.Qualities.Quality.WEBDL720p))
                                                      .Build()
                           });

            var result = Subject.AugmentQuality(_localEpisode, _downloadClientItem);
            
            result.Should().NotBe(null);
            result.Source.Should().Be(QualitySource.Web);
        }
    }
}
