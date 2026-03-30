namespace SanitizeService.Application;

public sealed class SanitizationOptions
{
    public const string SectionName = "Sanitization";

    public const long DefaultMaxFileSizeBytes = 100L * 1024 * 1024;

    /// <summary>
    /// Default ceiling for the raw HTTP request body (Kestrel / multipart). Set high enough to cover multipart
    /// overhead around the largest file upload any endpoint will allow.
    /// </summary>
    public const long DefaultMaxRequestBodyBytes = DefaultMaxFileSizeBytes + 256L * 1024;

    /// <summary>
    /// Maximum uploaded <strong>file</strong> size for the sanitize flow (controller, <see cref="SeekableStreamEnsurer"/>).
    /// Other endpoints can use different limits later while <see cref="MaxRequestBodyBytes"/> remains the host-wide ceiling.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;

    /// <summary>
    /// Host-wide maximum HTTP request body size (Kestrel <c>MaxRequestBodySize</c> and form <c>MultipartBodyLengthLimit</c>).
    /// Independent of <see cref="MaxFileSizeBytes"/>; should be &gt;= largest per-route file limit plus multipart envelope.
    /// </summary>
    public long MaxRequestBodyBytes { get; set; } = DefaultMaxRequestBodyBytes;

    /// <summary>
    /// ASCII header signature for ABC files.
    /// </summary>
    public string AbcHeaderSignature { get; set; } = "123";

    /// <summary>
    /// ASCII footer signature for ABC files.
    /// </summary>
    public string AbcFooterSignature { get; set; } = "789";

    /// <summary>
    /// ABC block size in bytes.
    /// </summary>
    public int AbcBlockSize { get; set; } = 3;

    /// <summary>
    /// Minimum allowed middle byte digit for a valid ABC block.
    /// </summary>
    public int AbcValidDigitMin { get; set; } = 1;

    /// <summary>
    /// Maximum allowed middle byte digit for a valid ABC block.
    /// </summary>
    public int AbcValidDigitMax { get; set; } = 9;
}
