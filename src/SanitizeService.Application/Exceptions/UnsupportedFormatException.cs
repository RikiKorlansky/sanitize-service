namespace SanitizeService.Application.Exceptions;

/// <summary>
/// Raised when the file content does not match any registered format.
/// </summary>
public sealed class UnsupportedFormatException : Exception
{
    public UnsupportedFormatException(string message) : base(message)
    {
    }
}
