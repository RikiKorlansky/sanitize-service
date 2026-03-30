using Microsoft.Extensions.Logging;

namespace SanitizeService.Domain;

/// <summary>
/// Detects ABC format using the configured header signature.
/// </summary>
public sealed class AbcFormatProbe : IFileFormatProbe
{
    private readonly AbcSanitizationSettings _settings;
    private readonly ILogger<AbcFormatProbe> _logger;

    public AbcFormatProbe(AbcSanitizationSettings settings, ILogger<AbcFormatProbe> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public int Priority => 10;

    public async Task<FileFormat?> TryDetectAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (!stream.CanSeek)
        {
            throw new InvalidOperationException(
                "Stream must be seekable for format detection. Ensure upstream buffering to a seekable stream.");
        }

        stream.Position = 0;
        var headerLength = _settings.HeaderSignature.Length;
        var buffer = new byte[headerLength];
        var read = await stream.ReadAsync(buffer.AsMemory(0, headerLength), cancellationToken);
        stream.Position = 0;

        if (read < headerLength)
        {
            _logger.LogDebug("ABC probe: stream shorter than header ({Read} bytes).", read);
            return null;
        }

        if (!buffer.AsSpan().SequenceEqual(_settings.HeaderSignature))
        {
            return null;
        }

        _logger.LogDebug("ABC format detected from configured header.");
        return FileFormat.Abc;
    }
}
