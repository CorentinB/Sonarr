using NzbDrone.Core.Configuration;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Config
{
    public class MetadataSourceConfigResource : RestResource
    {
        public string TmdbApiKey { get; set; }
        public bool TmdbDefaultForNewShows { get; set; }
    }

    public static class MetadataSourceConfigResourceMapper
    {
        public static MetadataSourceConfigResource ToResource(IConfigService model)
        {
            return new MetadataSourceConfigResource
            {
                TmdbApiKey = model.TmdbApiKey,
                TmdbDefaultForNewShows = model.TmdbDefaultForNewShows
            };
        }
    }
}
