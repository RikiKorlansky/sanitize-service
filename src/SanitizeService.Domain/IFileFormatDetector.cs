namespace SanitizeService.Domain;

/// <summary>
/// Application entry for format detection (typically implemented by <c>CompositeFileFormatDetector</c> in Application).
/// Individual formats implement <see cref="IFileFormatProbe"/> instead; probes are composed via DI.
/// </summary>
public interface IFileFormatDetector
{
    /// <summary>Unused for the composite facade; kept for interface consistency.</summary>
    int Priority { get; }

    /// <summary>
    /// Returns the format when a registered probe matches; otherwise null. Seekable streams are reset by probes
    /// and by the composite between attempts.
    /// </summary>
    Task<FileFormat?> TryDetectAsync(Stream stream, CancellationToken cancellationToken);
}
