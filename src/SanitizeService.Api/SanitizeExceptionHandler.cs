using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SanitizeService.Application.Exceptions;
using SanitizeService.Domain.Exceptions;

namespace SanitizeService.Api;

/// <summary>
/// Maps domain and application exceptions to HTTP status codes. Unhandled exceptions become 500 without leaking details.
/// </summary>
public sealed class SanitizeExceptionHandler : IExceptionHandler
{
    private readonly ILogger<SanitizeExceptionHandler> _logger;

    public SanitizeExceptionHandler(ILogger<SanitizeExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            return false;
        }

        int status;

        switch (exception)
        {
            case UnsupportedFormatException:
                status = StatusCodes.Status415UnsupportedMediaType;
                _logger.LogWarning(exception, "Request failed with status {Status}.", status);
                break;
            case InvalidAbcStructureException:
                status = StatusCodes.Status400BadRequest;
                _logger.LogWarning(exception, "Request failed with status {Status}.", status);
                break;
            case FileSizeExceededException:
                status = StatusCodes.Status413PayloadTooLarge;
                _logger.LogWarning(exception, "Request failed with status {Status}.", status);
                break;
            default:
                status = StatusCodes.Status500InternalServerError;
                _logger.LogError(exception, "Unhandled exception.");
                break;
        }

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";

        var title = ReasonPhrases.GetReasonPhrase(status);
        if (string.IsNullOrEmpty(title))
        {
            title = status == StatusCodes.Status500InternalServerError
                ? "Internal Server Error"
                : "Error";
        }

        string? detail = exception switch
        {
            UnsupportedFormatException e => string.IsNullOrWhiteSpace(e.Message)
                ? "The file format is not supported."
                : e.Message,
            InvalidAbcStructureException e => string.IsNullOrWhiteSpace(e.Message)
                ? "The file does not match the expected ABC structure."
                : e.Message,
            FileSizeExceededException e => string.IsNullOrWhiteSpace(e.Message)
                ? "The file exceeds the maximum allowed size."
                : e.Message,
            _ => null,
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
        };

        if (detail is not null)
        {
            problem.Detail = detail;
        }

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
