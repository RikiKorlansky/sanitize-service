using System.Text;

namespace SanitizeService.Domain;

public sealed class AbcSanitizationSettings
{
    public required byte[] HeaderSignature { get; init; }
    public required byte[] FooterSignature { get; init; }
    public required int BlockSize { get; init; }
    public required byte ValidMiddleByteMin { get; init; }
    public required byte ValidMiddleByteMax { get; init; }

    public static AbcSanitizationSettings Create(
        string headerSignature,
        string footerSignature,
        int blockSize,
        int validDigitMin,
        int validDigitMax)
    {
        if (string.IsNullOrWhiteSpace(headerSignature))
        {
            throw new InvalidOperationException("ABC header signature must be configured.");
        }

        if (string.IsNullOrWhiteSpace(footerSignature))
        {
            throw new InvalidOperationException("ABC footer signature must be configured.");
        }

        if (blockSize < 3)
        {
            throw new InvalidOperationException("ABC block size must be at least 3.");
        }

        if (validDigitMin < 0 || validDigitMax > 9 || validDigitMin > validDigitMax)
        {
            throw new InvalidOperationException("ABC valid digit range must be between 0 and 9.");
        }

        return new AbcSanitizationSettings
        {
            HeaderSignature = Encoding.ASCII.GetBytes(headerSignature),
            FooterSignature = Encoding.ASCII.GetBytes(footerSignature),
            BlockSize = blockSize,
            ValidMiddleByteMin = (byte)('0' + validDigitMin),
            ValidMiddleByteMax = (byte)('0' + validDigitMax),
        };
    }
}
