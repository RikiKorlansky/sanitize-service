using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SanitizeService.Application;
using SanitizeService.Domain;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers sanitization services. To add a format: register <see cref="IFileFormatProbe"/> + <see cref="IFileSanitizer"/>,
    /// e.g. <c>services.AddSingleton&lt;IFileFormatProbe, MyFormatProbe&gt;();</c> and <c>AddSingleton&lt;IFileSanitizer, MySanitizer&gt;()</c>.
    /// </summary>
    public static IServiceCollection AddSanitization(this IServiceCollection services)
    {
        services.AddSingleton<SeekableStreamEnsurer>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SanitizationOptions>>().Value;
            return AbcSanitizationSettings.Create(
                options.AbcHeaderSignature,
                options.AbcFooterSignature,
                options.AbcBlockSize,
                options.AbcValidDigitMin,
                options.AbcValidDigitMax);
        });

        services.AddSingleton<IFileFormatProbe, AbcFormatProbe>();
        services.AddSingleton<IFileFormatDetector, CompositeFileFormatDetector>();

        services.AddSingleton<IFileSanitizer, AbcFileSanitizer>();
        services.AddSingleton<IFileSanitizerFactory, FileSanitizerFactory>();
        services.AddSingleton<ISanitizationService, SanitizationService>();

        return services;
    }
}
