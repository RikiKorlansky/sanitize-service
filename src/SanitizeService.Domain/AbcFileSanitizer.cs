using Microsoft.Extensions.Logging;
using SanitizeService.Domain.Exceptions;

namespace SanitizeService.Domain;

/// <summary>
/// ABC: header "123", footer "789" (only the last 3 bytes of the file), body is zero or more strict 3-byte blocks A[1-9]C.
/// Invalid blocks are replaced with the 5-byte sequence "A255C". Body length must be an exact multiple of 3 bytes.
/// </summary>
public sealed class AbcFileSanitizer : IFileSanitizer
{
    private static readonly byte[] ReplacementBytes = "A255C"u8.ToArray();

    private static readonly byte[] HeaderBytes = "123"u8.ToArray();
    private static readonly byte[] FooterBytes = "789"u8.ToArray();

    private readonly ILogger<AbcFileSanitizer> _logger;

    public AbcFileSanitizer(ILogger<AbcFileSanitizer> logger)
    {
        _logger = logger;
    }

    public FileFormat SupportedFormat => FileFormat.Abc;

    public async Task<Stream> SanitizeAsync(Stream input, CancellationToken cancellationToken)
    {
        // Load full content into a buffer so header, body, and footer are disjoint slices — the footer is never
        // read while scanning the body, and nothing past body[index] is treated as a block.
        var data = await LoadAllBytesAsync(input, cancellationToken);
        var totalLength = data.Length;

        if (totalLength < 6)
        {
            throw new InvalidAbcStructureException($"ABC file must be at least 6 bytes (header + footer). Length={totalLength}.");
        }

        var bodyLength = totalLength - 6;
        if (bodyLength % 3 != 0)
        {
            throw new InvalidAbcStructureException(
                $"ABC body (between header and footer) must be a whole number of 3-byte blocks. Body length={bodyLength}.");
        }

        if (!BytesEqualAt(data, 0, HeaderBytes))
        {
            throw new InvalidAbcStructureException("ABC header must be ASCII \"123\".");
        }

        if (!BytesEqualAt(data, totalLength - 3, FooterBytes))
        {
            throw new InvalidAbcStructureException("ABC footer must be ASCII \"789\".");
        }

        var tempPath = Path.GetTempFileName();
        var output = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 65536,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

        try
        {
            await output.WriteAsync(HeaderBytes, cancellationToken);

            var replacements = 0;
            const int bodyStart = 3;
            for (var i = 0; i < bodyLength; i += 3)
            {
                var off = bodyStart + i;
                if (IsValidAbcBlock(data, off))
                {
                    await output.WriteAsync(data.AsMemory(off, 3), cancellationToken);
                }
                else
                {
                    await output.WriteAsync(ReplacementBytes, cancellationToken);
                    replacements++;
                }
            }

            await output.WriteAsync(FooterBytes, cancellationToken);
            await output.FlushAsync(cancellationToken);

            if (replacements > 0)
            {
                _logger.LogInformation("ABC sanitization applied {Count} A255C replacement(s) for invalid middle content.", replacements);
            }
            else
            {
                _logger.LogInformation("ABC middle section contained only valid A[1-9]C blocks.");
            }

            output.Position = 0;
            return output;
        }
        catch
        {
            await output.DisposeAsync();
            throw;
        }
    }

    private static async Task<byte[]> LoadAllBytesAsync(Stream input, CancellationToken cancellationToken)
    {
        // Length is only defined for seekable streams; never call it when CanSeek is false.
        if (input.CanSeek)
        {
            input.Position = 0;
            long len;
            try
            {
                len = input.Length;
            }
            catch (NotSupportedException)
            {
                return await CopyToBufferViaMemoryStreamAsync(input, cancellationToken);
            }

            if (len > int.MaxValue)
            {
                throw new InvalidOperationException("ABC file exceeds maximum supported size.");
            }

            var n = (int)len;
            var buffer = new byte[n];
            if (n > 0)
            {
                await input.ReadExactlyAsync(buffer.AsMemory(0, n), cancellationToken);
            }

            return buffer;
        }

        return await CopyToBufferViaMemoryStreamAsync(input, cancellationToken);
    }

    /// <summary>
    /// Buffers the entire stream without using <see cref="Stream.Length"/> (non-seekable streams, or seekable
    /// streams that do not expose length). Position is reset on the buffer stream after copying.
    /// </summary>
    private static async Task<byte[]> CopyToBufferViaMemoryStreamAsync(Stream input, CancellationToken cancellationToken)
    {
        await using var ms = new MemoryStream();
        await input.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        if (ms.Length > int.MaxValue)
        {
            throw new InvalidOperationException("ABC file exceeds maximum supported size.");
        }

        return ms.ToArray();
    }

    private static bool BytesEqualAt(byte[] data, int offset, byte[] pattern)
    {
        for (var i = 0; i < pattern.Length; i++)
        {
            if (data[offset + i] != pattern[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidAbcBlock(byte[] data, int start)
    {
        var mid = data[start + 1];
        return data[start] == (byte)'A'
            && data[start + 2] == (byte)'C'
            && mid >= (byte)'1'
            && mid <= (byte)'9';
    }
}
