namespace SanitizeService.Domain.Exceptions;

/// <summary>
/// Thrown when an ABC file fails structural validation (header, footer, or truncated content).
/// </summary>
public sealed class InvalidAbcStructureException : Exception
{
    public InvalidAbcStructureException(string message) : base(message)
    {
    }
}
