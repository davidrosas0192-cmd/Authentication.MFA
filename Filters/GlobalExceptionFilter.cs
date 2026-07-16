using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Authentication.Fido2.Filters;

/// <summary>
/// Global exception filter that catches unhandled exceptions across all endpoints,
/// logs them, and returns a consistent 500 ProblemDetails response.
///
/// NOTE: Endpoints with specific exception handling (e.g. catch UnauthorizedAccessException
/// in Fido2Controller.CompleteLogin) retain their own catch blocks for those cases.
/// This filter only handles exceptions that escape all controller-level catch blocks.
/// </summary>
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(
            context.Exception,
            "Unhandled exception on {Method} {Path}",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path
        );

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = "An unexpected error occurred. Please try again later.",
        })
        {
            StatusCode = StatusCodes.Status500InternalServerError,
        };

        context.ExceptionHandled = true;
    }
}
