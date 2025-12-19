using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.Tmdb.Resource
{
    public class TmdbTvShowResource
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("original_name")]
        public string OriginalName { get; set; }

        [JsonProperty("overview")]
        public string Overview { get; set; }

        [JsonProperty("tagline")]
        public string Tagline { get; set; }

        [JsonProperty("first_air_date")]
        public string FirstAirDate { get; set; }

        [JsonProperty("last_air_date")]
        public string LastAirDate { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("original_language")]
        public string OriginalLanguage { get; set; }

        [JsonProperty("homepage")]
        public string Homepage { get; set; }

        [JsonProperty("in_production")]
        public bool InProduction { get; set; }

        [JsonProperty("number_of_episodes")]
        public int NumberOfEpisodes { get; set; }

        [JsonProperty("number_of_seasons")]
        public int NumberOfSeasons { get; set; }

        [JsonProperty("episode_run_time")]
        public List<int> EpisodeRunTime { get; set; }

        [JsonProperty("vote_average")]
        public double VoteAverage { get; set; }

        [JsonProperty("vote_count")]
        public int VoteCount { get; set; }

        [JsonProperty("popularity")]
        public double Popularity { get; set; }

        [JsonProperty("poster_path")]
        public string PosterPath { get; set; }

        [JsonProperty("backdrop_path")]
        public string BackdropPath { get; set; }

        [JsonProperty("genres")]
        public List<TmdbGenreResource> Genres { get; set; }

        [JsonProperty("networks")]
        public List<TmdbNetworkResource> Networks { get; set; }

        [JsonProperty("created_by")]
        public List<TmdbCreatorResource> CreatedBy { get; set; }

        [JsonProperty("seasons")]
        public List<TmdbSeasonSummaryResource> Seasons { get; set; }

        [JsonProperty("external_ids")]
        public TmdbExternalIdsResource ExternalIds { get; set; }

        [JsonProperty("content_ratings")]
        public TmdbContentRatingsResource ContentRatings { get; set; }

        [JsonProperty("credits")]
        public TmdbCreditsResource Credits { get; set; }

        [JsonProperty("images")]
        public TmdbImagesResource Images { get; set; }

        [JsonProperty("alternative_titles")]
        public TmdbAlternativeTitlesResource AlternativeTitles { get; set; }

        [JsonProperty("origin_country")]
        public List<string> OriginCountry { get; set; }

        [JsonProperty("languages")]
        public List<string> Languages { get; set; }

        [JsonProperty("production_companies")]
        public List<TmdbProductionCompanyResource> ProductionCompanies { get; set; }
    }

    public class TmdbGenreResource
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class TmdbNetworkResource
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("logo_path")]
        public string LogoPath { get; set; }

        [JsonProperty("origin_country")]
        public string OriginCountry { get; set; }
    }

    public class TmdbCreatorResource
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("profile_path")]
        public string ProfilePath { get; set; }
    }

    public class TmdbProductionCompanyResource
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("logo_path")]
        public string LogoPath { get; set; }

        [JsonProperty("origin_country")]
        public string OriginCountry { get; set; }
    }

    public class TmdbContentRatingsResource
    {
        [JsonProperty("results")]
        public List<TmdbContentRatingResource> Results { get; set; }
    }

    public class TmdbContentRatingResource
    {
        [JsonProperty("iso_3166_1")]
        public string Iso31661 { get; set; }

        [JsonProperty("rating")]
        public string Rating { get; set; }
    }

    public class TmdbCreditsResource
    {
        [JsonProperty("cast")]
        public List<TmdbCastResource> Cast { get; set; }

        [JsonProperty("crew")]
        public List<TmdbCrewResource> Crew { get; set; }
    }

    public class TmdbCastResource
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("character")]
        public string Character { get; set; }

        [JsonProperty("profile_path")]
        public string ProfilePath { get; set; }

        [JsonProperty("order")]
        public int Order { get; set; }
    }

    public class TmdbCrewResource
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("job")]
        public string Job { get; set; }

        [JsonProperty("department")]
        public string Department { get; set; }

        [JsonProperty("profile_path")]
        public string ProfilePath { get; set; }
    }

    public class TmdbAlternativeTitlesResource
    {
        [JsonProperty("results")]
        public List<TmdbAlternativeTitleResource> Results { get; set; }
    }

    public class TmdbAlternativeTitleResource
    {
        [JsonProperty("iso_3166_1")]
        public string Iso31661 { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
