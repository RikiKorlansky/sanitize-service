namespace SanitizeService.Domain;

/// <summary>
/// Format-specific sanitizer. Implementations read from the provided <see cref="Stream"/> and return a new stream
/// containing sanitized output. The ABC sanitizer requires a seekable stream with a known length and processes the body
/// in fixed-size chunks without loading the whole input; other formats may define different constraints.
/// </summary>
public interface IFileSanitizer
{
    FileFormat SupportedFormat { get; }

    Task<Stream> SanitizeAsync(Stream input, CancellationToken cancellationToken);
}
