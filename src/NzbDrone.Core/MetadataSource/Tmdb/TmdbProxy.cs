using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
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
        private const int MaxActorsToImport = 15;
        private const int MaxRetries = 3;
        private const int BaseRetryDelayMs = 1000;

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

        private HttpResponse<T> ExecuteWithRetry<T>(HttpRequest request, string operationDescription)
            where T : new()
        {
            var lastException = default(Exception);

            for (var attempt = 0; attempt < MaxRetries; attempt++)
            {
                try
                {
                    var response = _httpClient.Get<T>(request);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        var delay = BaseRetryDelayMs * (int)Math.Pow(2, attempt);
                        _logger.Warn("TMDB rate limit hit for {0}, waiting {1}ms before retry (attempt {2}/{3})",
                            operationDescription, delay, attempt + 1, MaxRetries);
                        Thread.Sleep(delay);
                        continue;
                    }

                    return response;
                }
                catch (HttpException ex) when (ex.Response?.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var delay = BaseRetryDelayMs * (int)Math.Pow(2, attempt);
                    _logger.Warn("TMDB rate limit hit for {0}, waiting {1}ms before retry (attempt {2}/{3})",
                        operationDescription, delay, attempt + 1, MaxRetries);
                    Thread.Sleep(delay);
                    lastException = ex;
                }
            }

            throw new TmdbException($"TMDB rate limit exceeded after {MaxRetries} retries for {operationDescription}", lastException);
        }

        private static DateTime? TryParseTmdbDate(string dateString)
        {
            if (dateString.IsNullOrWhiteSpace())
            {
                return null;
            }

            if (DateTime.TryParseExact(
                dateString,
                "yyyy-MM-dd",
                DateTimeFormatInfo.InvariantInfo,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            {
                return parsed;
            }

            return null;
        }

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
            if (tmdbId <= 0)
            {
                throw new ArgumentException($"Invalid TMDB ID: {tmdbId}", nameof(tmdbId));
            }

            try
            {
                var httpRequest = _requestBuilder.Create()
                    .Resource($"tv/{tmdbId}")
                    .AddQueryParam("append_to_response", "external_ids,content_ratings,credits")
                    .Build();

                httpRequest.AllowAutoRedirect = true;
                httpRequest.SuppressHttpError = true;

                var httpResponse = ExecuteWithRetry<TmdbTvShowResource>(httpRequest, $"GetSeriesInfo({tmdbId})");

                if (httpResponse.HasHttpError)
                {
                    if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        throw new TmdbException(HttpStatusCode.NotFound, $"Series with TMDB ID {tmdbId} not found");
                    }

                    throw new TmdbException($"TMDB API returned error: {httpResponse.StatusCode}");
                }

                var show = httpResponse.Resource;

                if (show == null)
                {
                    throw new TmdbException($"TMDB returned empty response for series ID {tmdbId}");
                }

                var series = MapSeries(show);

                // Fetch episodes for each season
                var episodes = new List<Episode>();
                var failedSeasons = new List<int>();

                foreach (var season in show.Seasons ?? new List<TmdbSeasonSummaryResource>())
                {
                    var seasonEpisodes = GetSeasonEpisodes(tmdbId, season.SeasonNumber);
                    if (seasonEpisodes == null)
                    {
                        failedSeasons.Add(season.SeasonNumber);
                    }
                    else
                    {
                        episodes.AddRange(seasonEpisodes);
                    }
                }

                if (failedSeasons.Any())
                {
                    _logger.Warn("Failed to fetch episodes for {0} season(s) of TMDB ID {1}: {2}",
                        failedSeasons.Count, tmdbId, string.Join(", ", failedSeasons));
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

                var httpResponse = ExecuteWithRetry<TmdbSeasonResource>(httpRequest, $"GetSeasonEpisodes({tmdbId}, S{seasonNumber})");

                if (httpResponse.HasHttpError)
                {
                    _logger.Error("Failed to get season {0} for TMDB ID {1}: HTTP {2}", seasonNumber, tmdbId, httpResponse.StatusCode);
                    return null;
                }

                return httpResponse.Resource?.Episodes?.Select(MapEpisode).ToList() ?? new List<Episode>();
            }
            catch (TmdbException ex)
            {
                _logger.Error(ex, "Failed to get season {0} for TMDB ID {1} after retries", seasonNumber, tmdbId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error getting season {0} for TMDB ID {1}", seasonNumber, tmdbId);
                return null;
            }
        }

        public List<Series> SearchForNewSeries(string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                throw new ArgumentException("Search title cannot be empty", nameof(title));
            }

            try
            {
                var httpRequest = _requestBuilder.Create()
                    .Resource("search/tv")
                    .AddQueryParam("query", title)
                    .Build();

                httpRequest.AllowAutoRedirect = true;
                httpRequest.SuppressHttpError = true;

                var httpResponse = ExecuteWithRetry<TmdbSearchResultResource>(httpRequest, $"SearchForNewSeries('{title}')");

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

            var firstAired = TryParseTmdbDate(result.FirstAirDate);
            if (firstAired.HasValue)
            {
                series.FirstAired = firstAired.Value;
                series.Year = firstAired.Value.Year;
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
            if (show == null)
            {
                throw new ArgumentNullException(nameof(show));
            }

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
            var firstAired = TryParseTmdbDate(show.FirstAirDate);
            if (firstAired.HasValue)
            {
                series.FirstAired = firstAired.Value;
                series.Year = firstAired.Value.Year;
            }

            // Last aired date
            var lastAired = TryParseTmdbDate(show.LastAirDate);
            if (lastAired.HasValue)
            {
                series.LastAired = lastAired.Value;
            }

            // Runtime
            if (show.EpisodeRunTime != null && show.EpisodeRunTime.Count > 0)
            {
                series.Runtime = show.EpisodeRunTime.First();
            }

            // Network
            if (show.Networks != null && show.Networks.Count > 0)
            {
                series.Network = show.Networks.First()?.Name ?? string.Empty;
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
                .Take(MaxActorsToImport)
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
            var airDateUtc = TryParseTmdbDate(tmdbEpisode.AirDate);
            if (airDateUtc.HasValue)
            {
                episode.AirDateUtc = airDateUtc.Value;
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

        private SeriesStatusType MapSeriesStatus(string status)
        {
            if (status.IsNullOrWhiteSpace())
            {
                return SeriesStatusType.Continuing;
            }

            var lowerStatus = status.ToLowerInvariant();
            return lowerStatus switch
            {
                "ended" => SeriesStatusType.Ended,
                "canceled" => SeriesStatusType.Ended,
                "returning series" => SeriesStatusType.Continuing,
                "in production" => SeriesStatusType.Continuing,
                "planned" => SeriesStatusType.Upcoming,
                _ => LogUnknownStatusAndReturnDefault(status)
            };
        }

        private SeriesStatusType LogUnknownStatusAndReturnDefault(string status)
        {
            _logger.Debug("Unknown TMDB series status '{0}', defaulting to Continuing", status);
            return SeriesStatusType.Continuing;
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
