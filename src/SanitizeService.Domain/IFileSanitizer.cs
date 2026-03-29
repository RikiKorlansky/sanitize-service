namespace SanitizeService.Domain;

/// <summary>
/// Format-specific sanitizer. Implementations read from the provided <see cref="Stream"/> and return a new stream
/// containing sanitized output. Buffering the full input (or large portions of it) is permitted when needed for
/// validation or format-specific processing; callers should assume memory use may scale with input size.
/// </summary>
public interface IFileSanitizer
{
    FileFormat SupportedFormat { get; }

    Task<Stream> SanitizeAsync(Stream input, CancellationToken cancellationToken);
}
