using SanitizeService.Domain;

namespace SanitizeService.Application;

/// <summary>
/// Facade implementing <see cref="IFileFormatDetector"/>: injects all <see cref="IFileFormatProbe"/> registrations,
/// orders by <see cref="IFileFormatProbe.Priority"/> (deterministic), and returns the first match. Before each probe,
/// seekable streams are rewound so every probe sees the stream from the start.
/// </summary>
public sealed class CompositeFileFormatDetector : IFileFormatDetector
{
    private readonly IReadOnlyList<IFileFormatProbe> _probes;

    public CompositeFileFormatDetector(IEnumerable<IFileFormatProbe> probes)
    {
        _probes = probes.OrderBy(p => p.Priority).ToList();
    }

    public int Priority => int.MinValue;

    public async Task<FileFormat?> TryDetectAsync(Stream stream, CancellationToken cancellationToken)
    {
        foreach (var probe in _probes)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            var format = await probe.TryDetectAsync(stream, cancellationToken);
            if (format is not null)
            {
                return format;
            }
        }

        return null;
    }
}
