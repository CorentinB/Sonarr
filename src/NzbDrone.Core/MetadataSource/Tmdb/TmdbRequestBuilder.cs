using System;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MetadataSource.Tmdb
{
    public interface ITmdbRequestBuilder
    {
        HttpRequestBuilder Create();
        string ImageBaseUrl { get; }
        bool IsConfigured { get; }
    }

    public class TmdbRequestBuilder : ITmdbRequestBuilder
    {
        private const string BaseUrl = "https://api.themoviedb.org/3/";
        private const string ImageBaseUrlSecure = "https://image.tmdb.org/t/p/";

        private readonly IConfigService _configService;

        public TmdbRequestBuilder(IConfigService configService)
        {
            _configService = configService;
        }

        public string ImageBaseUrl => ImageBaseUrlSecure;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_configService.TmdbApiKey);

        public HttpRequestBuilder Create()
        {
            var apiKey = _configService.TmdbApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("TMDB API key is not configured. Please set your TMDB API key in Settings > Metadata Source.");
            }

            return new HttpRequestBuilder(BaseUrl)
                .AddQueryParam("api_key", apiKey);
        }
    }
}
