using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.Tmdb.Resource
{
    public class TmdbImageResource
    {
        [JsonProperty("aspect_ratio")]
        public double AspectRatio { get; set; }

        [JsonProperty("file_path")]
        public string FilePath { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("vote_average")]
        public double VoteAverage { get; set; }

        [JsonProperty("vote_count")]
        public int VoteCount { get; set; }

        [JsonProperty("iso_639_1")]
        public string Iso6391 { get; set; }
    }

    public class TmdbImagesResource
    {
        [JsonProperty("backdrops")]
        public TmdbImageResource[] Backdrops { get; set; }

        [JsonProperty("posters")]
        public TmdbImageResource[] Posters { get; set; }

        [JsonProperty("logos")]
        public TmdbImageResource[] Logos { get; set; }
    }
}
