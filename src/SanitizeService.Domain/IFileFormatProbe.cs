namespace SanitizeService.Domain;

/// <summary>
/// Single-format probe aggregated by the application composite detector. Register each implementation as
/// <c>IFileFormatProbe</c>; the composite resolves all probes from DI, orders by <see cref="Priority"/>, and tries
/// each until one matches — so new formats add a probe class, a sanitizer, and one registration line.
/// </summary>
public interface IFileFormatProbe
{
    /// <summary>Lower values run first.</summary>
    int Priority { get; }

    /// <summary>
    /// Returns the format when this probe matches; otherwise null. When the stream is seekable, implementations
    /// should reset <see cref="Stream.Position"/> to 0 before returning; the composite also rewinds before each probe.
    /// </summary>
    Task<FileFormat?> TryDetectAsync(Stream stream, CancellationToken cancellationToken);
}
