using System.Text;
using Microsoft.Extensions.Logging;

namespace SanitizeService.Domain;

/// <summary>
/// Detects ABC format when the first three bytes are ASCII "123".
/// </summary>
public sealed class AbcFormatProbe : IFileFormatProbe
{
    private static ReadOnlySpan<byte> Header => "123"u8;
    private readonly ILogger<AbcFormatProbe> _logger;

    public AbcFormatProbe(ILogger<AbcFormatProbe> logger)
    {
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
        var buffer = new byte[3];
        var read = await stream.ReadAsync(buffer.AsMemory(0, 3), cancellationToken);
        stream.Position = 0;

        if (read < 3)
        {
            _logger.LogDebug("ABC probe: stream shorter than header ({Read} bytes).", read);
            return null;
        }

        if (!Header.SequenceEqual(buffer))
        {
            return null;
        }

        _logger.LogDebug("ABC format detected from header {Header}.", Encoding.ASCII.GetString(buffer));
        return FileFormat.Abc;
    }
}
