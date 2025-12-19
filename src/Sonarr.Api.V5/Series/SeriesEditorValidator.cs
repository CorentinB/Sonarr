using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

namespace Sonarr.Api.V5.Series;

public class SeriesEditorValidator : AbstractValidator<NzbDrone.Core.Tv.Series>
{
    public SeriesEditorValidator(
        RootFolderExistsValidator rootFolderExistsValidator,
        QualityProfileExistsValidator qualityProfileExistsValidator,
        IConfigService configService)
    {
        RuleFor(s => s.RootFolderPath).Cascade(CascadeMode.Stop)
            .IsValidPath()
            .SetValidator(rootFolderExistsValidator)
            .When(s => s.RootFolderPath.IsNotNullOrWhiteSpace());

        RuleFor(c => c.QualityProfileId).Cascade(CascadeMode.Stop)
            .ValidId()
            .SetValidator(qualityProfileExistsValidator);

        // Validate MetadataSource changes
        RuleFor(c => c.MetadataSource)
            .Must(_ => configService.TmdbApiKey.IsNotNullOrWhiteSpace())
            .When(c => c.MetadataSource == MetadataSource.Tmdb)
            .WithMessage("Cannot use TMDB as metadata source without configuring a TMDB API key");

        RuleFor(c => c.TmdbId)
            .GreaterThan(0)
            .When(c => c.MetadataSource == MetadataSource.Tmdb && c.TvdbId <= 0)
            .WithMessage("TMDB-only series must have a valid TMDB ID");
    }
}
