using Microsoft.Extensions.Logging;
using SanitizeService.Application.Exceptions;
using SanitizeService.Domain;

namespace SanitizeService.Application;

public sealed class SanitizationService : ISanitizationService
{
    private readonly SeekableStreamEnsurer _seekableStreamEnsurer;
    private readonly IFileFormatDetector _formatDetector;
    private readonly IFileSanitizerFactory _sanitizerFactory;
    private readonly ILogger<SanitizationService> _logger;

    public SanitizationService(
        SeekableStreamEnsurer seekableStreamEnsurer,
        IFileFormatDetector formatDetector,
        IFileSanitizerFactory sanitizerFactory,
        ILogger<SanitizationService> logger)
    {
        _seekableStreamEnsurer = seekableStreamEnsurer;
        _formatDetector = formatDetector;
        _sanitizerFactory = sanitizerFactory;
        _logger = logger;
    }

    public async Task<Stream> SanitizeAsync(Stream input, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sanitization started.");

        await using var handle = await _seekableStreamEnsurer.EnsureSeekableAsync(input, cancellationToken);

        var stream = handle.Stream;
        var format = await _formatDetector.TryDetectAsync(stream, cancellationToken);

        if (format is null || format == FileFormat.Unknown)
        {
            throw new UnsupportedFormatException("Could not detect a supported file format from the content.");
        }

        _logger.LogInformation("Detected format {Format}.", format);

        stream.Position = 0;
        var sanitizer = _sanitizerFactory.GetSanitizer(format.Value);
        return await sanitizer.SanitizeAsync(stream, cancellationToken);
    }
}
