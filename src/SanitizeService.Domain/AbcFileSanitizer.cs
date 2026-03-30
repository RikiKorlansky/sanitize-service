using System.Buffers;
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
    /// <summary>Read buffer size for body streaming (not tied to block size).</summary>
    private const int BodyChunkSize = 64 * 1024;

    private static readonly byte[] ReplacementBytes = "A255C"u8.ToArray();
    private readonly AbcSanitizationSettings _settings;
    private readonly ILogger<AbcFileSanitizer> _logger;

    public AbcFileSanitizer(AbcSanitizationSettings settings, ILogger<AbcFileSanitizer> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public FileFormat SupportedFormat => FileFormat.Abc;

    /// <summary>
    /// Requires a seekable input with known <see cref="Stream.Length"/> so the body span is known without buffering the whole file.
    /// Sanitization is one forward pass: header → body (chunked) → footer tail (validated once at end).
    /// </summary>
    public async Task<Stream> SanitizeAsync(Stream input, CancellationToken cancellationToken)
    {
        if (!TryGetSeekableLength(input, out var totalLength))
        {
            throw new InvalidOperationException(
                "ABC sanitization requires a seekable stream with a known length. Buffer the stream (e.g. via SeekableStreamEnsurer) before calling the sanitizer.");
        }

        return await SanitizeForwardStreamingAsync(input, totalLength, cancellationToken);
    }

    /// <summary>
    /// Single forward pass: validate header, stream-transform body without loading it entirely, then read and validate the footer tail.
    /// </summary>
    private async Task<Stream> SanitizeForwardStreamingAsync(
        Stream input,
        long totalLength,
        CancellationToken cancellationToken)
    {
        var headerLength = _settings.HeaderSignature.Length;
        var footerLength = _settings.FooterSignature.Length;
        var blockSize = _settings.BlockSize;
        var minimumLength = headerLength + footerLength;

        if (totalLength < minimumLength)
        {
            throw new InvalidAbcStructureException(
                $"ABC file must be at least {minimumLength} bytes (header + footer). Length={totalLength}.");
        }

        var bodyLength = totalLength - minimumLength;
        if (bodyLength % blockSize != 0)
        {
            throw new InvalidAbcStructureException(
                $"ABC body (between header and footer) must be a whole number of {blockSize}-byte blocks. Body length={bodyLength}.");
        }

        input.Position = 0;
        var headerBuf = ArrayPool<byte>.Shared.Rent(headerLength);
        try
        {
            await input.ReadExactlyAsync(headerBuf.AsMemory(0, headerLength), cancellationToken);
            if (!headerBuf.AsSpan(0, headerLength).SequenceEqual(_settings.HeaderSignature))
            {
                throw new InvalidAbcStructureException("ABC header does not match the configured signature.");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuf);
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

            // Body only: footer bytes are never fed into the block parser — they are consumed only in the tail step below.
            input.Position = headerLength;
            var replacements = await WriteSanitizedBodyStreamingAsync(
                input,
                output,
                bodyLength,
                blockSize,
                cancellationToken);

            // Tail: last footerLength bytes of the file, read sequentially after the body (not as ABC blocks).
            var footerBuf = ArrayPool<byte>.Shared.Rent(footerLength);
            try
            {
                await input.ReadExactlyAsync(footerBuf.AsMemory(0, footerLength), cancellationToken);
                if (!footerBuf.AsSpan(0, footerLength).SequenceEqual(_settings.FooterSignature))
                {
                    throw new InvalidAbcStructureException("ABC footer does not match the configured signature.");
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(footerBuf);
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

    /// <summary>
    /// Reads exactly <paramref name="bodyLength"/> bytes from <paramref name="input"/> in chunks, emitting sanitized blocks.
    /// Uses a bounded working buffer (carry for partial blocks + chunk); never holds the whole body in memory.
    /// </summary>
    private async Task<int> WriteSanitizedBodyStreamingAsync(
        Stream input,
        Stream output,
        long bodyLength,
        int blockSize,
        CancellationToken cancellationToken)
    {
        if (bodyLength == 0)
        {
            return 0;
        }

        var rentLength = BodyChunkSize + blockSize - 1;
        var buffer = ArrayPool<byte>.Shared.Rent(rentLength);
        var replacements = 0;
        var carry = 0;
        var remaining = bodyLength;

        try
        {
            while (remaining > 0)
            {
                var space = rentLength - carry;
                var toRead = (int)Math.Min(space, remaining);
                await input.ReadExactlyAsync(buffer.AsMemory(carry, toRead), cancellationToken);
                remaining -= toRead;
                var available = carry + toRead;

                var fullBlocks = available / blockSize;
                for (var b = 0; b < fullBlocks; b++)
                {
                    var blockOffset = b * blockSize;
                    if (IsValidAbcBlock(buffer, blockOffset, blockSize))
                    {
                        await output.WriteAsync(buffer.AsMemory(blockOffset, blockSize), cancellationToken);
                    }
                    else
                    {
                        await output.WriteAsync(ReplacementBytes, cancellationToken);
                        replacements++;
                    }
                }

                var consumed = fullBlocks * blockSize;
                var leftover = available - consumed;
                if (leftover > 0)
                {
                    buffer.AsSpan(consumed, leftover).CopyTo(buffer.AsSpan(0, leftover));
                }

                carry = leftover;
            }

            if (carry != 0)
            {
                throw new InvalidAbcStructureException(
                    $"ABC body streaming ended with {carry} trailing byte(s); expected a multiple of {blockSize}.");
            }

            return replacements;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool TryGetSeekableLength(Stream input, out long totalLength)
    {
        totalLength = 0;
        if (!input.CanSeek)
        {
            return false;
        }

        try
        {
            totalLength = input.Length;
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private bool IsValidAbcBlock(byte[] data, int offset, int blockSize)
    {
        var middleByte = data[offset + 1];
        return data[offset] == (byte)'A'
            && data[offset + blockSize - 1] == (byte)'C'
            && middleByte >= _settings.ValidMiddleByteMin
            && middleByte <= _settings.ValidMiddleByteMax;
    }
}
