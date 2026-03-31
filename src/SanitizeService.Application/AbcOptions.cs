namespace SanitizeService.Application;

public sealed class AbcOptions
{
    public const string SectionName = "Abc";

    /// <summary>
    /// ASCII header signature for ABC files.
    /// </summary>
    public string HeaderSignature { get; set; } = "123";

    /// <summary>
    /// ASCII footer signature for ABC files.
    /// </summary>
    public string FooterSignature { get; set; } = "789";

    /// <summary>
    /// ABC block size in bytes.
    /// </summary>
    public int BlockSize { get; set; } = 3;

    /// <summary>
    /// Minimum allowed middle byte digit for a valid ABC block.
    /// </summary>
    public int ValidDigitMin { get; set; } = 1;

    /// <summary>
    /// Maximum allowed middle byte digit for a valid ABC block.
    /// </summary>
    public int ValidDigitMax { get; set; } = 9;
}
