using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.Tmdb.Resource;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.Tmdb
{
    public interface ITmdbProxy
    {
        Tuple<Series, List<Episode>> GetSeriesInfo(int tmdbId);
        Tuple<Series, List<Episode>> GetSeriesInfoByTvdbId(int tvdbId);
        List<Series> SearchForNewSeries(string title);
        int? FindTmdbIdByTvdbId(int tvdbId);
        bool IsConfigured { get; }
    }

    public class TmdbProxy : ITmdbProxy
    {
        private readonly IHttpClient _httpClient;
        private readonly ITmdbRequestBuilder _requestBuilder;
        private readonly ISeriesService _seriesService;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public TmdbProxy(IHttpClient httpClient,
                         ITmdbRequestBuilder requestBuilder,
                         ISeriesService seriesService,
                         IConfigService configService,
                         Logger logger)
        {
            _httpClient = httpClient;
            _requestBuilder = requestBuilder;
            _seriesService = seriesService;
            _configService = configService;
            _logger = logger;
        }

        public bool IsConfigured => _requestBuilder.IsConfigured;

        public int? FindTmdbIdByTvdbId(int tvdbId)
        {
            try
            {
                var httpRequest = _requestBuilder.Create()
                    .Resource($"find/{tvdbId}")
                    .AddQueryParam("external_source", "tvdb_id")
                    .Build();

                httpRequest.AllowAutoRedirect = true;
                httpRequest.SuppressHttpError = true;

                var httpResponse = _httpClient.Get<TmdbFindResource>(httpRequest);

                if (httpResponse.HasHttpError)
                {
                    _logger.Warn("TMDB find by TVDB ID {0} returned error: {1}", tvdbId, httpResponse.StatusCode);
                    return null;
                }

                var tvResult = httpResponse.Resource?.TvResults?.FirstOrDefault();
                return tvResult?.Id;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to find TMDB ID for TVDB ID {0}", tvdbId);
                return null;
            }
        }

        public Tuple<Series, List<Episode>> GetSeriesInfoByTvdbId(int tvdbId)
        {
            var tmdbId = FindTmdbIdByTvdbId(tvdbId);

            if (!tmdbId.HasValue)
            {
                throw new SeriesNotFoundException(tvdbId);
            }

            return GetSeriesInfo(tmdbId.Value);
        }

        public Tuple<Series, List<Episode>> GetSeriesInfo(int tmdbId)
        {
            try
            {
                var httpRequest = _requestBuilder.Create()
                    .Resource($"tv/{tmdbId}")
                    .AddQueryParam("append_to_response", "external_ids,content_ratings,credits")
                    .Build();

                httpRequest.AllowAutoRedirect = true;
                httpRequest.SuppressHttpError = true;

                var httpResponse = _httpClient.Get<TmdbTvShowResource>(httpRequest);

                if (httpResponse.HasHttpError)
                {
                    if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        throw new TmdbException(HttpStatusCode.NotFound, $"Series with TMDB ID {tmdbId} not found");
                    }

                    throw new TmdbException($"TMDB API returned error: {httpResponse.StatusCode}");
                }

                var show = httpResponse.Resource;
                var series = MapSeries(show);

                // Fetch episodes for each season
                var episodes = new List<Episode>();
                foreach (var season in show.Seasons ?? new List<TmdbSeasonSummaryResource>())
                {
                    var seasonEpisodes = GetSeasonEpisodes(tmdbId, season.SeasonNumber);
                    episodes.AddRange(seasonEpisodes);
                }

                return new Tuple<Series, List<Episode>>(series, episodes);
            }
            catch (TmdbException)
            {
                throw;
            }
            catch (HttpException ex)
            {
                _logger.Warn(ex, "Failed to get series info from TMDB for ID {0}", tmdbId);
                throw new TmdbException("Failed to communicate with TMDB API", ex);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error getting series info from TMDB for ID {0}", tmdbId);
                throw new TmdbException("Unexpected error communicating with TMDB API", ex);
            }
        }

        private List<Episode> GetSeasonEpisodes(int tmdbId, int seasonNumber)
        {
            try
            {
                var httpRequest = _requestBuilder.Create()
                    .Resource($"tv/{tmdbId}/season/{seasonNumber}")
                    .Build();

                httpRequest.AllowAutoRedirect = true;
                httpRequest.SuppressHttpError = true;

                var httpResponse = _httpClient.Get<TmdbSeasonResource>(httpRequest);

                if (httpResponse.HasHttpError)
                {
                    _logger.Warn("Failed to get season {0} for TMDB ID {1}: {2}", seasonNumber, tmdbId, httpResponse.StatusCode);
                    return new List<Episode>();
                }

                return httpResponse.Resource?.Episodes?.Select(MapEpisode).ToList() ?? new List<Episode>();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to get season {0} for TMDB ID {1}", seasonNumber, tmdbId);
                return new List<Episode>();
            }
        }

        public List<Series> SearchForNewSeries(string title)
        {
            try
            {
                var httpRequest = _requestBuilder.Create()
                    .Resource("search/tv")
                    .AddQueryParam("query", title)
                    .Build();

                httpRequest.AllowAutoRedirect = true;
                httpRequest.SuppressHttpError = true;

                var httpResponse = _httpClient.Get<TmdbSearchResultResource>(httpRequest);

                if (httpResponse.HasHttpError)
                {
                    throw new TmdbException($"TMDB search returned error: {httpResponse.StatusCode}");
                }

                return httpResponse.Resource?.Results?.Select(MapSearchResult).ToList() ?? new List<Series>();
            }
            catch (TmdbException)
            {
                throw;
            }
            catch (HttpException ex)
            {
                _logger.Warn(ex, "Search for '{0}' failed on TMDB", title);
                throw new TmdbException("Search for '{0}' failed. Unable to communicate with TMDB.", ex, title);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Search for '{0}' failed on TMDB", title);
                throw new TmdbException("Search for '{0}' failed. Invalid response from TMDB.", ex, title);
            }
        }

        private Series MapSearchResult(TmdbSearchTvResultResource result)
        {
            // Check if series already exists in database
            var existingSeries = _seriesService.FindByTmdbId(result.Id);
            if (existingSeries != null)
            {
                return existingSeries;
            }

            return MapSearchResultToSeries(result);
        }

        private Series MapSearchResultToSeries(TmdbSearchTvResultResource result)
        {
            var series = new Series
            {
                TmdbId = result.Id,
                Title = result.Name,
                CleanTitle = Parser.Parser.CleanSeriesTitle(result.Name),
                SortTitle = SeriesTitleNormalizer.Normalize(result.Name, 0),
                Overview = result.Overview,
                Monitored = true
            };

            series.OriginalLanguage = result.OriginalLanguage.IsNotNullOrWhiteSpace()
                ? IsoLanguages.Find(result.OriginalLanguage.ToLower())?.Language ?? Language.English
                : Language.English;

            if (result.FirstAirDate.IsNotNullOrWhiteSpace())
            {
                if (DateTime.TryParseExact(
                    result.FirstAirDate,
                    "yyyy-MM-dd",
                    DateTimeFormatInfo.InvariantInfo,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var firstAired))
                {
                    series.FirstAired = firstAired;
                    series.Year = firstAired.Year;
                }
            }

            series.Ratings = new Ratings
            {
                Value = (decimal)result.VoteAverage,
                Votes = result.VoteCount
            };

            series.Images = new List<MediaCover.MediaCover>();

            if (result.PosterPath.IsNotNullOrWhiteSpace())
            {
                series.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Poster, GetImageUrl(result.PosterPath)));
            }

            if (result.BackdropPath.IsNotNullOrWhiteSpace())
            {
                series.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Fanart, GetImageUrl(result.BackdropPath, "original")));
            }

            return series;
        }

        private Series MapSeries(TmdbTvShowResource show)
        {
            var series = new Series
            {
                TmdbId = show.Id,
                Title = show.Name,
                CleanTitle = Parser.Parser.CleanSeriesTitle(show.Name),
                Overview = show.Overview,
                Monitored = true
            };

            // Set external IDs
            if (show.ExternalIds != null)
            {
                if (show.ExternalIds.TvdbId.HasValue)
                {
                    series.TvdbId = show.ExternalIds.TvdbId.Value;
                }

                if (show.ExternalIds.TvRageId.HasValue)
                {
                    series.TvRageId = show.ExternalIds.TvRageId.Value;
                }

                series.ImdbId = show.ExternalIds.ImdbId;
            }

            series.SortTitle = SeriesTitleNormalizer.Normalize(show.Name, series.TvdbId);
            series.TitleSlug = GenerateSlug(show.Name, show.Id);

            series.OriginalLanguage = show.OriginalLanguage.IsNotNullOrWhiteSpace()
                ? IsoLanguages.Find(show.OriginalLanguage.ToLower())?.Language ?? Language.English
                : Language.English;

            // First aired date
            if (show.FirstAirDate.IsNotNullOrWhiteSpace())
            {
                if (DateTime.TryParseExact(
                    show.FirstAirDate,
                    "yyyy-MM-dd",
                    DateTimeFormatInfo.InvariantInfo,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var firstAired))
                {
                    series.FirstAired = firstAired;
                    series.Year = firstAired.Year;
                }
            }

            // Last aired date
            if (show.LastAirDate.IsNotNullOrWhiteSpace())
            {
                if (DateTime.TryParseExact(
                    show.LastAirDate,
                    "yyyy-MM-dd",
                    DateTimeFormatInfo.InvariantInfo,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var lastAired))
                {
                    series.LastAired = lastAired;
                }
            }

            // Runtime
            if (show.EpisodeRunTime != null && show.EpisodeRunTime.Count > 0)
            {
                series.Runtime = show.EpisodeRunTime.First();
            }

            // Network
            if (show.Networks != null && show.Networks.Count > 0)
            {
                series.Network = show.Networks.First().Name;
            }

            // Status
            series.Status = MapSeriesStatus(show.Status);

            // Genres
            series.Genres = show.Genres?.Select(g => g.Name).ToList() ?? new List<string>();

            // Ratings
            series.Ratings = new Ratings
            {
                Value = (decimal)show.VoteAverage,
                Votes = show.VoteCount
            };

            // Content rating / certification
            if (show.ContentRatings?.Results != null)
            {
                var usRating = show.ContentRatings.Results.FirstOrDefault(r => r.Iso31661 == "US");
                if (usRating != null)
                {
                    series.Certification = usRating.Rating;
                }
            }

            // Actors
            series.Actors = MapActors(show.Credits);

            // Seasons
            series.Seasons = show.Seasons?.Select(MapSeason).ToList() ?? new List<Season>();

            // Images
            series.Images = new List<MediaCover.MediaCover>();

            if (show.PosterPath.IsNotNullOrWhiteSpace())
            {
                series.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Poster, GetImageUrl(show.PosterPath)));
            }

            if (show.BackdropPath.IsNotNullOrWhiteSpace())
            {
                series.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Fanart, GetImageUrl(show.BackdropPath, "original")));
            }

            return series;
        }

        private List<Actor> MapActors(TmdbCreditsResource credits)
        {
            if (credits?.Cast == null)
            {
                return new List<Actor>();
            }

            return credits.Cast
                .OrderBy(c => c.Order)
                .Take(15)
                .Select(c => new Actor
                {
                    Name = c.Name,
                    Character = c.Character,
                    Images = c.ProfilePath.IsNotNullOrWhiteSpace()
                        ? new List<MediaCover.MediaCover> { new MediaCover.MediaCover(MediaCoverTypes.Headshot, GetImageUrl(c.ProfilePath, "w185")) }
                        : new List<MediaCover.MediaCover>()
                })
                .ToList();
        }

        private Season MapSeason(TmdbSeasonSummaryResource season)
        {
            var mapped = new Season
            {
                SeasonNumber = season.SeasonNumber,
                Monitored = season.SeasonNumber > 0,
                Images = new List<MediaCover.MediaCover>()
            };

            if (season.PosterPath.IsNotNullOrWhiteSpace())
            {
                mapped.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Poster, GetImageUrl(season.PosterPath)));
            }

            return mapped;
        }

        private Episode MapEpisode(TmdbEpisodeResource tmdbEpisode)
        {
            var episode = new Episode
            {
                TmdbId = tmdbEpisode.Id,
                SeasonNumber = tmdbEpisode.SeasonNumber,
                EpisodeNumber = tmdbEpisode.EpisodeNumber,
                Title = tmdbEpisode.Name,
                Overview = tmdbEpisode.Overview,
                AirDate = tmdbEpisode.AirDate,
                Runtime = tmdbEpisode.Runtime ?? 0
            };

            // Parse air date to UTC
            if (tmdbEpisode.AirDate.IsNotNullOrWhiteSpace())
            {
                if (DateTime.TryParseExact(
                    tmdbEpisode.AirDate,
                    "yyyy-MM-dd",
                    DateTimeFormatInfo.InvariantInfo,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var airDateUtc))
                {
                    episode.AirDateUtc = airDateUtc;
                }
            }

            // Ratings
            episode.Ratings = new Ratings
            {
                Value = (decimal)tmdbEpisode.VoteAverage,
                Votes = tmdbEpisode.VoteCount
            };

            // Episode image (still)
            if (tmdbEpisode.StillPath.IsNotNullOrWhiteSpace())
            {
                episode.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Screenshot, GetImageUrl(tmdbEpisode.StillPath, "w300")));
            }

            // Map episode type to finale type
            if (tmdbEpisode.EpisodeType.IsNotNullOrWhiteSpace())
            {
                episode.FinaleType = MapEpisodeType(tmdbEpisode.EpisodeType);
            }

            return episode;
        }

        private static SeriesStatusType MapSeriesStatus(string status)
        {
            if (status.IsNullOrWhiteSpace())
            {
                return SeriesStatusType.Continuing;
            }

            return status.ToLowerInvariant() switch
            {
                "ended" => SeriesStatusType.Ended,
                "canceled" => SeriesStatusType.Ended,
                "returning series" => SeriesStatusType.Continuing,
                "in production" => SeriesStatusType.Continuing,
                "planned" => SeriesStatusType.Upcoming,
                _ => SeriesStatusType.Continuing
            };
        }

        private static string MapEpisodeType(string episodeType)
        {
            return episodeType?.ToLowerInvariant() switch
            {
                "finale" => "series",
                "mid_season" => null,
                "standard" => null,
                _ => null
            };
        }

        private string GetImageUrl(string path, string size = "w500")
        {
            if (path.IsNullOrWhiteSpace())
            {
                return null;
            }

            return $"{_requestBuilder.ImageBaseUrl}{size}{path}";
        }

        private static string GenerateSlug(string title, int tmdbId)
        {
            var slug = title.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("'", "")
                .Replace(".", "")
                .Replace(":", "");

            // Remove any non-alphanumeric characters except hyphens
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");

            // Remove consecutive hyphens
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");

            return $"{slug}-{tmdbId}";
        }
    }
}
