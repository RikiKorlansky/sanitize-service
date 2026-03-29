namespace SanitizeService.Domain;

/// <summary>
/// Known file formats. New formats are added here; detectors and sanitizers register via DI.
/// </summary>
public enum FileFormat
{
    Unknown = 0,
    Abc = 1,
}
