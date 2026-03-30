using Microsoft.Extensions.Logging;
using SanitizeService.Domain.Exceptions;

namespace SanitizeService.Domain;

/// <summary>
/// Sanitizes ABC payloads using configured header/footer signatures, block size, and middle-byte valid range.
/// Invalid blocks are replaced with the 5-byte sequence "A255C".
/// </summary>
public sealed class AbcFileSanitizer : IFileSanitizer
{
    private const int StreamBufferSize = 64 * 1024;
    private static readonly byte[] ReplacementBytes = "A255C"u8.ToArray();
    private readonly AbcSanitizationSettings _settings;
    private readonly ILogger<AbcFileSanitizer> _logger;

    public AbcFileSanitizer(AbcSanitizationSettings settings, ILogger<AbcFileSanitizer> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public FileFormat SupportedFormat => FileFormat.Abc;

    public async Task<Stream> SanitizeAsync(Stream input, CancellationToken cancellationToken)
    {
        // Load full content into a buffer so header, body, and footer are disjoint slices — the footer is never
        // read while scanning the body, and nothing past body[index] is treated as a block.
        var data = await LoadAllBytesAsync(input, cancellationToken);
        var totalLength = data.Length;
        var headerLength = _settings.HeaderSignature.Length;
        var footerLength = _settings.FooterSignature.Length;
        var minimumLength = headerLength + footerLength;

        if (totalLength < minimumLength)
        {
            throw new InvalidAbcStructureException(
                $"ABC file must be at least {minimumLength} bytes (header + footer). Length={totalLength}.");
        }

        var bodyLength = totalLength - minimumLength;
        if (bodyLength % _settings.BlockSize != 0)
        {
            throw new InvalidAbcStructureException(
                $"ABC body (between header and footer) must be a whole number of {_settings.BlockSize}-byte blocks. Body length={bodyLength}.");
        }

        if (!data.AsSpan(0, headerLength).SequenceEqual(_settings.HeaderSignature))
        {
            throw new InvalidAbcStructureException("ABC header does not match the configured signature.");
        }

        var footerOffset = totalLength - footerLength;
        if (!data.AsSpan(footerOffset, footerLength).SequenceEqual(_settings.FooterSignature))
        {
            throw new InvalidAbcStructureException("ABC footer does not match the configured signature.");
        }

        var tempPath = Path.GetTempFileName();
        var output = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: StreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

        try
        {
            await output.WriteAsync(_settings.HeaderSignature, cancellationToken);

            var replacements = 0;
            var bodyStart = headerLength;
            var blockCount = bodyLength / _settings.BlockSize;
            // Parse each fixed-size block from the body span and sanitize invalid ones.
            for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                var blockOffset = bodyStart + (blockIndex * _settings.BlockSize);
                if (IsValidAbcBlock(data, blockOffset))
                {
                    await output.WriteAsync(data.AsMemory(blockOffset, _settings.BlockSize), cancellationToken);
                }
                else
                {
                    await output.WriteAsync(ReplacementBytes, cancellationToken);
                    replacements++;
                }
            }

            await output.WriteAsync(_settings.FooterSignature, cancellationToken);
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
            try
            {
                await output.DisposeAsync();
            }
            catch (Exception disposeEx)
            {
                _logger.LogWarning(disposeEx, "Failed to dispose temporary output stream after sanitization failure.");
            }

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

    private bool IsValidAbcBlock(byte[] data, int blockOffset)
    {
        var middleByteOffset = blockOffset + 1;
        var blockLastByteOffset = blockOffset + _settings.BlockSize - 1;
        var middleByte = data[middleByteOffset];
        return data[blockOffset] == (byte)'A'
            && data[blockLastByteOffset] == (byte)'C'
            && middleByte >= _settings.ValidMiddleByteMin
            && middleByte <= _settings.ValidMiddleByteMax;
    }
}
