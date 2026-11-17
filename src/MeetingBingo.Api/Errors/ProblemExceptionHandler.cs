using Microsoft.AspNetCore.Diagnostics;

namespace MeetingBingo.Api.Errors;

/// <summary>Turns any unhandled exception into a plain 500 problem response, and logs the details on our side only.</summary>
internal sealed class ProblemExceptionHandler(IProblemDetailsService problems, ILogger<ProblemExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken cancellationToken)
    {
        log.LogError(exception, "Unhandled exception on {Method} {Path}", http.Request.Method, http.Request.Path);
        http.Response.StatusCode = StatusCodes.Status500InternalServerError;
        // The exception is not passed on: no stack trace or internal message reaches the client
        return await problems.TryWriteAsync(new()
        {
            HttpContext = http,
            ProblemDetails = { Status = 500, Title = "Algo ha fallado", Detail = "Vuelve a intentarlo en un momento." },
        });
    }
}
