using System.Text.RegularExpressions;
using FluentValidation;
using NzbDrone.Core.Configuration;
using Sonarr.Http;

namespace Sonarr.Api.V3.Config
{
    [V3ApiController("config/metadatasource")]
    public class MetadataSourceConfigController : ConfigController<MetadataSourceConfigResource>
    {
        public MetadataSourceConfigController(IConfigService configService)
            : base(configService)
        {
            SharedValidator.RuleFor(c => c.TmdbApiKey)
                .Matches(@"^[a-f0-9]{32}$", RegexOptions.IgnoreCase)
                .When(c => !string.IsNullOrEmpty(c.TmdbApiKey))
                .WithMessage("Invalid TMDB API key format. Key should be 32 hexadecimal characters.");
        }

        protected override MetadataSourceConfigResource ToResource(IConfigService model)
        {
            return MetadataSourceConfigResourceMapper.ToResource(model);
        }
    }
}
