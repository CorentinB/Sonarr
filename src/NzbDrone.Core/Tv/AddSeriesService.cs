using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.Tmdb;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Tv
{
    public interface IAddSeriesService
    {
        Series AddSeries(Series newSeries);
        List<Series> AddSeries(List<Series> newSeries, bool ignoreErrors = false);
    }

    public class AddSeriesService : IAddSeriesService
    {
        private readonly ISeriesService _seriesService;
        private readonly IProvideSeriesInfo _seriesInfo;
        private readonly ITmdbProxy _tmdbProxy;
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly IAddSeriesValidator _addSeriesValidator;
        private readonly IConfigService _configService;
        private readonly ISceneMappingRepository _sceneMappingRepository;
        private readonly Logger _logger;

        public AddSeriesService(ISeriesService seriesService,
                                IProvideSeriesInfo seriesInfo,
                                ITmdbProxy tmdbProxy,
                                IBuildFileNames fileNameBuilder,
                                IAddSeriesValidator addSeriesValidator,
                                IConfigService configService,
                                ISceneMappingRepository sceneMappingRepository,
                                Logger logger)
        {
            _seriesService = seriesService;
            _seriesInfo = seriesInfo;
            _tmdbProxy = tmdbProxy;
            _fileNameBuilder = fileNameBuilder;
            _addSeriesValidator = addSeriesValidator;
            _configService = configService;
            _sceneMappingRepository = sceneMappingRepository;
            _logger = logger;
        }

        public Series AddSeries(Series newSeries)
        {
            Ensure.That(newSeries, () => newSeries).IsNotNull();

            newSeries = AddSkyhookData(newSeries);
            newSeries = SetPropertiesAndValidate(newSeries);

            _logger.Info("Adding Series {0} Path: [{1}]", newSeries, newSeries.Path);
            _seriesService.AddSeries(newSeries);

            // Store TMDB alternate titles for TMDB-only series
            if (newSeries.TmdbId > 0 && newSeries.TvdbId <= 0)
            {
                StoreTmdbAlternateTitles(newSeries);
            }

            return newSeries;
        }

        public List<Series> AddSeries(List<Series> newSeries, bool ignoreErrors = false)
        {
            var added = DateTime.UtcNow;
            var seriesToAdd = new List<Series>();
            var existingSeriesTvdbIds = _seriesService.AllSeriesTvdbIds();
            var existingSeriesTmdbIds = _seriesService.AllSeriesTmdbIds();

            foreach (var s in newSeries)
            {
                if (s.Path.IsNullOrWhiteSpace())
                {
                    _logger.Info("Adding Series {0} Root Folder Path: [{1}]", s, s.RootFolderPath);
                }
                else
                {
                    _logger.Info("Adding Series {0} Path: [{1}]", s, s.Path);
                }

                try
                {
                    var series = AddSkyhookData(s);
                    series = SetPropertiesAndValidate(series);
                    series.Added = added;

                    // Only check for duplicate TvdbId if it's a real TVDB ID (> 0)
                    // TvdbId = 0 means TMDB-only series
                    if (series.TvdbId > 0 && existingSeriesTvdbIds.Any(f => f == series.TvdbId))
                    {
                        _logger.Debug("TVDB ID {0} was not added due to validation failure: Series {1} already exists in database", s.TvdbId, s);
                        continue;
                    }

                    // Check for duplicate TmdbId for TMDB-only series (TvdbId = 0)
                    if (series.TvdbId == 0 && series.TmdbId > 0 && existingSeriesTmdbIds.Any(f => f == series.TmdbId))
                    {
                        _logger.Debug("TMDB ID {0} was not added due to validation failure: Series {1} already exists in database", series.TmdbId, s);
                        continue;
                    }

                    if (series.TvdbId > 0 && seriesToAdd.Any(f => f.TvdbId == series.TvdbId))
                    {
                        _logger.Trace("TVDB ID {0} was already added from another import list, not adding series {1} again", s.TvdbId, s);
                        continue;
                    }

                    // Check for duplicate TmdbId in series being added (TMDB-only series)
                    if (series.TvdbId == 0 && series.TmdbId > 0 && seriesToAdd.Any(f => f.TvdbId == 0 && f.TmdbId == series.TmdbId))
                    {
                        _logger.Trace("TMDB ID {0} was already added from another import list, not adding series {1} again", series.TmdbId, s);
                        continue;
                    }

                    var duplicateSlug = seriesToAdd.FirstOrDefault(f => f.TitleSlug == series.TitleSlug);
                    if (duplicateSlug != null)
                    {
                        _logger.Debug("TVDB ID {0} was not added due to validation failure: Duplicate Slug {1} used by series {2}", s.TvdbId, s.TitleSlug, duplicateSlug.TvdbId);
                        continue;
                    }

                    seriesToAdd.Add(series);
                }
                catch (ValidationException ex)
                {
                    if (!ignoreErrors)
                    {
                        throw;
                    }

                    _logger.Debug("Series {0} with TVDB ID {1} was not added due to validation failures. {2}", s, s.TvdbId, ex.Message);
                }
            }

            var addedSeries = _seriesService.AddSeries(seriesToAdd);

            // Store TMDB alternate titles for TMDB-only series
            foreach (var series in addedSeries.Where(s => s.TmdbId > 0 && s.TvdbId <= 0))
            {
                StoreTmdbAlternateTitles(series);
            }

            return addedSeries;
        }

        private Series AddSkyhookData(Series newSeries)
        {
            Tuple<Series, List<Episode>> tuple;

            try
            {
                // Handle TMDB-only series (from TMDB search results which only have TmdbId)
                if (newSeries.TvdbId <= 0 && newSeries.TmdbId > 0)
                {
                    _logger.Debug("Fetching series info from TMDB for TmdbId {0}", newSeries.TmdbId);
                    tuple = _tmdbProxy.GetSeriesInfo(newSeries.TmdbId);
                }
                else
                {
                    tuple = _seriesInfo.GetSeriesInfo(newSeries.TvdbId);
                }
            }
            catch (SeriesNotFoundException)
            {
                var idType = newSeries.TvdbId > 0 ? "TVDB" : "TMDB";
                var idValue = newSeries.TvdbId > 0 ? newSeries.TvdbId : newSeries.TmdbId;
                _logger.Error(
                    "Series {0} with {1} ID {2} was not found. Path: {3}",
                    newSeries,
                    idType,
                    idValue,
                    newSeries.Path);

                throw new ValidationException(new List<ValidationFailure>
                                              {
                                                  new ValidationFailure(idType + "Id", $"A series with this ID was not found. Path: {newSeries.Path}", idValue)
                                              });
            }
            catch (TmdbException ex)
            {
                _logger.Error(ex, "TMDB lookup failed for series {0} with TmdbId {1}. Path: {2}", newSeries, newSeries.TmdbId, newSeries.Path);

                throw new ValidationException(new List<ValidationFailure>
                                              {
                                                  new ValidationFailure("TmdbId", $"TMDB lookup failed: {ex.Message}. Path: {newSeries.Path}", newSeries.TmdbId)
                                              });
            }

            var series = tuple.Item1;

            // If seasons were passed in on the new series use them, otherwise use the seasons from Skyhook
            newSeries.Seasons = newSeries.Seasons != null && newSeries.Seasons.Any() ? newSeries.Seasons : series.Seasons;

            series.ApplyChanges(newSeries);

            return series;
        }

        private Series SetPropertiesAndValidate(Series newSeries)
        {
            if (string.IsNullOrWhiteSpace(newSeries.Path))
            {
                var folderName = _fileNameBuilder.GetSeriesFolder(newSeries);
                newSeries.Path = Path.Combine(newSeries.RootFolderPath, folderName);
            }

            newSeries.CleanTitle = newSeries.Title.CleanSeriesTitle();
            newSeries.SortTitle = SeriesTitleNormalizer.Normalize(newSeries.Title, newSeries.TvdbId);
            newSeries.Added = DateTime.UtcNow;

            // Set metadata source:
            // - TMDB-only series (no TVDB ID) must use TMDB
            // - Otherwise use TMDB if configured as default
            if (newSeries.TvdbId <= 0 && newSeries.TmdbId > 0)
            {
                newSeries.MetadataSource = MetadataSource.Tmdb;
            }
            else if (_configService.TmdbDefaultForNewShows && _configService.TmdbApiKey.IsNotNullOrWhiteSpace())
            {
                newSeries.MetadataSource = MetadataSource.Tmdb;
            }

            if (newSeries.AddOptions != null && newSeries.AddOptions.Monitor == MonitorTypes.None)
            {
                newSeries.Monitored = false;
            }

            var validationResult = _addSeriesValidator.Validate(newSeries);

            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            return newSeries;
        }

        private void StoreTmdbAlternateTitles(Series series)
        {
            try
            {
                if (!_tmdbProxy.IsConfigured)
                {
                    return;
                }

                var alternateTitles = _tmdbProxy.GetAlternateTitles(series.TmdbId);

                if (alternateTitles == null || alternateTitles.Count == 0)
                {
                    _logger.Debug("No alternate titles found for TMDB ID {0}", series.TmdbId);
                    return;
                }

                _logger.Info("Storing {0} alternate title(s) for series '{1}' (TMDB ID: {2})", alternateTitles.Count, series.Title, series.TmdbId);

                var sceneMappings = alternateTitles.Select(title => new SceneMapping
                {
                    Title = title,
                    ParseTerm = title.CleanSeriesTitle(),
                    SearchTerm = title,
                    TvdbId = 0,
                    TmdbId = series.TmdbId,
                    SceneOrigin = "tmdb",
                    Type = "TmdbAlternateTitle"
                }).ToList();

                _sceneMappingRepository.InsertMany(sceneMappings);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to store TMDB alternate titles for series '{0}' (TMDB ID: {1})", series.Title, series.TmdbId);
            }
        }
    }
}
