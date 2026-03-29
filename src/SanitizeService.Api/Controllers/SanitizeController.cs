using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SanitizeService.Application;
using SanitizeService.Api.Requests;

namespace SanitizeService.Api.Controllers;

[ApiController]
[Route("")]
public sealed class SanitizeController : ControllerBase
{
    private readonly ISanitizationService _sanitizationService;
    private readonly SanitizationOptions _options;
    private readonly ILogger<SanitizeController> _logger;

    public SanitizeController(
        ISanitizationService sanitizationService,
        IOptions<SanitizationOptions> options,
        ILogger<SanitizeController> logger)
    {
        _sanitizationService = sanitizationService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Accepts a file, detects format from bytes, and returns the sanitized stream.</summary>
    /// <remarks>Uses <see cref="SanitizationOptions.MaxFileSizeBytes"/> for this upload; other actions may use different limits.</remarks>
    [HttpPost("sanitize")]
    [Consumes("multipart/form-data")]
    [DisableRequestSizeLimit]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> SanitizeAsync([FromForm] SanitizeRequest request, CancellationToken cancellationToken)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
        {
            return Problem(detail: "A non-empty file is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > _options.MaxFileSizeBytes)
        {
            return Problem(detail: "File exceeds the configured maximum size.", statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        _logger.LogInformation(
            "Received file {Name}, length {Length} bytes, content type {ContentType}.",
            file.FileName,
            file.Length,
            file.ContentType);

        await using var input = file.OpenReadStream();
        var output = await _sanitizationService.SanitizeAsync(input, cancellationToken);
        var downloadName = string.IsNullOrWhiteSpace(file.FileName) ? "sanitized.bin" : $"sanitized-{file.FileName}";
        return File(output, "application/octet-stream", fileDownloadName: downloadName);
    }
}
