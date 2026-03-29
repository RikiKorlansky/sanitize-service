using SanitizeService.Application.Exceptions;
using SanitizeService.Domain;

namespace SanitizeService.Application;

/// <summary>
/// Resolves sanitizers registered in DI. New formats add a new <see cref="IFileSanitizer"/> implementation and registration only.
/// </summary>
public sealed class FileSanitizerFactory : IFileSanitizerFactory
{
    private readonly IReadOnlyDictionary<FileFormat, IFileSanitizer> _sanitizers;

    public FileSanitizerFactory(IEnumerable<IFileSanitizer> sanitizers)
    {
        _sanitizers = sanitizers
            .GroupBy(s => s.SupportedFormat)
            .ToDictionary(g => g.Key, g => g.Single());
    }

    public IFileSanitizer GetSanitizer(FileFormat format)
    {
        if (_sanitizers.TryGetValue(format, out var sanitizer))
        {
            return sanitizer;
        }

        throw new UnsupportedFormatException($"No sanitizer is registered for format '{format}'.");
    }
}
