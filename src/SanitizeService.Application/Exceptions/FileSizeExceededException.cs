namespace SanitizeService.Application.Exceptions;

/// <summary>
/// Raised when input exceeds configured maximum size.
/// </summary>
public sealed class FileSizeExceededException : Exception
{
    public FileSizeExceededException(string message, long attemptedSize) : base(message)
    {
        AttemptedSize = attemptedSize;
    }

    public long AttemptedSize { get; }
}
