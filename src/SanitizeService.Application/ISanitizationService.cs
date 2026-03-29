namespace SanitizeService.Application;

/// <summary>
/// Application entry point: ensure seekable input, detect format, sanitize, return output stream.
/// </summary>
public interface ISanitizationService
{
    Task<Stream> SanitizeAsync(Stream input, CancellationToken cancellationToken);
}
