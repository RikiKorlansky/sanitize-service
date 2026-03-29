using Microsoft.Extensions.Logging.Abstractions;
using SanitizeService.Application;
using SanitizeService.Domain;

namespace SanitizeService.Tests;

public sealed class CompositeFileFormatDetectorTests
{
    /// <summary>
    /// Leaves the stream offset after reading; composite must still rewind before the next probe.
    /// </summary>
    private sealed class NonRewindingProbe : IFileFormatProbe
    {
        public int Priority => 0;

        public Task<FileFormat?> TryDetectAsync(Stream stream, CancellationToken cancellationToken)
        {
            _ = stream.ReadByte();
            return Task.FromResult<FileFormat?>(null);
        }
    }

    [Fact]
    public async Task Rewinds_seekable_stream_before_each_probe_so_next_probe_sees_header()
    {
        var inner = new MemoryStream("123789"u8.ToArray());
        IFileFormatProbe[] probes =
        [
            new NonRewindingProbe(),
            new AbcFormatProbe(NullLogger<AbcFormatProbe>.Instance),
        ];
        var composite = new CompositeFileFormatDetector(probes);

        var format = await composite.TryDetectAsync(inner, CancellationToken.None);

        Assert.Equal(FileFormat.Abc, format);
        Assert.Equal(0, inner.Position);
    }
}
