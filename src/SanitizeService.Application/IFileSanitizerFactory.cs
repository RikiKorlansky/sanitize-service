using SanitizeService.Domain;

namespace SanitizeService.Application;

/// <summary>
/// Resolves the sanitizer for a detected format. New formats register new <see cref="IFileSanitizer"/> implementations in DI.
/// </summary>
public interface IFileSanitizerFactory
{
    IFileSanitizer GetSanitizer(FileFormat format);
}
