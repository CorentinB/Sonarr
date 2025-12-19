using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MetadataSource.SkyHook;
using NzbDrone.Core.MetadataSource.Tmdb;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource
{
    public class MetadataProviderService : IProvideSeriesInfo, ISearchForNewSeries
    {
        private readonly ITmdbProxy _tmdbProxy;
        private readonly ISkyHookProxy _skyHookProxy;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public MetadataProviderService(ITmdbProxy tmdbProxy,
                                       ISkyHookProxy skyHookProxy,
                                       IConfigService configService,
                                       Logger logger)
        {
            _tmdbProxy = tmdbProxy;
            _skyHookProxy = skyHookProxy;
            _configService = configService;
            _logger = logger;
        }

        private bool IsTmdbConfigured => _configService.TmdbApiKey.IsNotNullOrWhiteSpace() &&
                                         _tmdbProxy.IsConfigured;

        // Used for search (new series) - checks global default setting
        private bool UseTmdbForNewSeries => _configService.TmdbDefaultForNewShows && IsTmdbConfigured;

        // Used for existing series refresh - checks per-series metadata source setting
        private bool ShouldUseTmdb(Series existingSeries)
        {
            if (existingSeries == null)
            {
                return false;
            }

            return existingSeries.MetadataSource == MetadataSource.Tmdb && IsTmdbConfigured;
        }

        public Tuple<Series, List<Episode>> GetSeriesInfo(int tvdbSeriesId)
        {
            // Without a series context, just use SkyHook
            return GetSeriesInfo(tvdbSeriesId, null);
        }

        public Tuple<Series, List<Episode>> GetSeriesInfo(int tvdbSeriesId, Series existingSeries)
        {
            if (ShouldUseTmdb(existingSeries))
            {
                try
                {
                    _logger.Debug("Fetching series info from TMDB for {0} (TVDB ID {1})", existingSeries.Title, tvdbSeriesId);

                    // Use existing TmdbId if available, otherwise look it up
                    var tmdbId = existingSeries.TmdbId > 0
                        ? existingSeries.TmdbId
                        : _tmdbProxy.FindTmdbIdByTvdbId(tvdbSeriesId);

                    if (tmdbId.HasValue)
                    {
                        // Validate season count before proceeding
                        var tmdbInfo = _tmdbProxy.GetSeriesInfo(tmdbId.Value);
                        var currentSeasonCount = existingSeries.Seasons.Count(s => s.SeasonNumber > 0);
                        var tmdbSeasonCount = tmdbInfo.Item1.Seasons.Count(s => s.SeasonNumber > 0);

                        if (currentSeasonCount != tmdbSeasonCount)
                        {
                            _logger.Warn(
                                "Season count mismatch for {0}: current {1}, TMDB {2}. Using SkyHook to prevent file mismatches.",
                                existingSeries.Title,
                                currentSeasonCount,
                                tmdbSeasonCount);
                            return _skyHookProxy.GetSeriesInfo(tvdbSeriesId);
                        }

                        return tmdbInfo;
                    }

                    _logger.Debug("Could not find TMDB ID for {0}, falling back to SkyHook", existingSeries.Title);
                }
                catch (TmdbException ex)
                {
                    _logger.Warn(ex, "TMDB lookup failed for {0}, falling back to SkyHook", existingSeries.Title);
                }
                catch (HttpException ex) when (ex.Response?.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.Warn("TMDB rate limit exceeded, falling back to SkyHook for {0}", existingSeries.Title);
                }
                catch (SeriesNotFoundException)
                {
                    _logger.Debug("Series {0} not found on TMDB, falling back to SkyHook", existingSeries.Title);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Unexpected error fetching from TMDB for {0}, falling back to SkyHook", existingSeries.Title);
                }
            }

            // Fall back to SkyHook
            _logger.Debug("Fetching series info from SkyHook for TVDB ID {0}", tvdbSeriesId);
            return _skyHookProxy.GetSeriesInfo(tvdbSeriesId);
        }

        public List<Series> SearchForNewSeries(string title)
        {
            if (UseTmdbForNewSeries)
            {
                try
                {
                    _logger.Debug("Searching for series on TMDB: {0}", title);
                    var results = _tmdbProxy.SearchForNewSeries(title);

                    if (results.Count > 0)
                    {
                        return results;
                    }

                    _logger.Debug("No results from TMDB for '{0}', falling back to SkyHook", title);
                }
                catch (TmdbException ex)
                {
                    _logger.Warn(ex, "TMDB search failed for '{0}', falling back to SkyHook", title);
                }
                catch (HttpException ex) when (ex.Response?.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.Warn("TMDB rate limit exceeded, falling back to SkyHook for search '{0}'", title);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Unexpected error searching TMDB for '{0}', falling back to SkyHook", title);
                }
            }

            // Fall back to SkyHook
            _logger.Debug("Searching for series on SkyHook: {0}", title);
            return _skyHookProxy.SearchForNewSeries(title);
        }

        public List<Series> SearchForNewSeriesByImdbId(string imdbId)
        {
            // TMDB supports searching by IMDB ID via their /find endpoint
            // For now, delegate to SkyHook which has better cross-reference support
            return _skyHookProxy.SearchForNewSeriesByImdbId(imdbId);
        }

        public List<Series> SearchForNewSeriesByAniListId(int aniListId)
        {
            // TMDB doesn't support AniList IDs
            return _skyHookProxy.SearchForNewSeriesByAniListId(aniListId);
        }

        public List<Series> SearchForNewSeriesByMyAnimeListId(int malId)
        {
            // TMDB doesn't support MyAnimeList IDs
            return _skyHookProxy.SearchForNewSeriesByMyAnimeListId(malId);
        }

        public List<Series> SearchForNewSeriesByTmdbId(int tmdbId)
        {
            if (IsTmdbConfigured)
            {
                try
                {
                    _logger.Debug("Fetching series from TMDB by TMDB ID {0}", tmdbId);
                    var result = _tmdbProxy.GetSeriesInfo(tmdbId);
                    return new List<Series> { result.Item1 };
                }
                catch (TmdbException ex)
                {
                    _logger.Warn(ex, "TMDB lookup failed for TMDB ID {0}, falling back to SkyHook", tmdbId);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Unexpected error fetching from TMDB for TMDB ID {0}, falling back to SkyHook", tmdbId);
                }
            }

            return _skyHookProxy.SearchForNewSeriesByTmdbId(tmdbId);
        }
    }
}
