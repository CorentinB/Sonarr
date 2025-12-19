using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.Tmdb.Resource
{
    public class TmdbFindResource
    {
        [JsonProperty("movie_results")]
        public List<object> MovieResults { get; set; }

        [JsonProperty("tv_results")]
        public List<TmdbFindTvResultResource> TvResults { get; set; }

        [JsonProperty("tv_episode_results")]
        public List<object> TvEpisodeResults { get; set; }

        [JsonProperty("tv_season_results")]
        public List<object> TvSeasonResults { get; set; }

        [JsonProperty("person_results")]
        public List<object> PersonResults { get; set; }
    }

    public class TmdbFindTvResultResource
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("original_name")]
        public string OriginalName { get; set; }

        [JsonProperty("overview")]
        public string Overview { get; set; }

        [JsonProperty("first_air_date")]
        public string FirstAirDate { get; set; }

        [JsonProperty("original_language")]
        public string OriginalLanguage { get; set; }

        [JsonProperty("poster_path")]
        public string PosterPath { get; set; }

        [JsonProperty("backdrop_path")]
        public string BackdropPath { get; set; }

        [JsonProperty("vote_average")]
        public double VoteAverage { get; set; }

        [JsonProperty("vote_count")]
        public int VoteCount { get; set; }

        [JsonProperty("popularity")]
        public double Popularity { get; set; }

        [JsonProperty("genre_ids")]
        public List<int> GenreIds { get; set; }

        [JsonProperty("origin_country")]
        public List<string> OriginCountry { get; set; }

        [JsonProperty("adult")]
        public bool Adult { get; set; }
    }
}
