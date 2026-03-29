using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SanitizeService.Application.Exceptions;

namespace SanitizeService.Application;

/// <summary>
/// Ensures a seekable stream for format detection and sanitization without buffering the entire payload in memory.
/// Non-seekable streams are copied to a temporary file on disk.
/// </summary>
public sealed class SeekableStreamEnsurer
{
    private readonly ILogger<SeekableStreamEnsurer> _logger;
    private readonly SanitizationOptions _options;

    public SeekableStreamEnsurer(IOptions<SanitizationOptions> options, ILogger<SeekableStreamEnsurer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SeekableStreamHandle> EnsureSeekableAsync(Stream input, CancellationToken cancellationToken)
    {
        var max = _options.MaxFileSizeBytes;

        if (input.CanSeek)
        {
            if (input.Length > max)
            {
                throw new FileSizeExceededException(
                    $"Input exceeds maximum allowed size of {max} bytes.",
                    input.Length);
            }

            return new SeekableStreamHandle(input, ownsStream: false);
        }

        _logger.LogInformation("Input stream is not seekable; spooling to a temporary file.");

        var tempPath = Path.GetTempFileName();
        await using (var file = new FileStream(
                         tempPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 65536,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            var buffer = new byte[65536];
            long total = 0;
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                total += read;
                if (total > max)
                {
                    throw new FileSizeExceededException(
                        $"Input exceeds maximum allowed size of {max} bytes.",
                        total);
                }

                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }

        var readStream = new FileStream(
            tempPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 65536,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        return new SeekableStreamHandle(readStream, ownsStream: true);
    }
}

/// <summary>
/// Holds a seekable stream and whether this layer owns disposal (temp file copy).
/// </summary>
public sealed class SeekableStreamHandle : IAsyncDisposable, IDisposable
{
    public SeekableStreamHandle(Stream stream, bool ownsStream)
    {
        Stream = stream;
        OwnsStream = ownsStream;
    }

    public Stream Stream { get; }

    public bool OwnsStream { get; }

    public void Dispose()
    {
        if (OwnsStream)
        {
            Stream.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (OwnsStream)
        {
            await Stream.DisposeAsync();
        }
    }
}
