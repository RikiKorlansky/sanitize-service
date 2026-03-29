using Microsoft.AspNetCore.Mvc;

namespace SanitizeService.Api.Requests;

public sealed class SanitizeRequest
{
    [FromForm(Name = "file")]
    public required IFormFile File { get; set; }
}
