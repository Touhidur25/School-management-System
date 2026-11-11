namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// Every error response has the same shape: { "error": { "code": "...", "message": "..." } }.
/// The UI maps `code` to a friendly message; `message` is a readable fallback.
/// </summary>
public class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            await WriteAsync(context, ex.StatusCode, ex.Code, ex.Message);
        }
        catch (BadHttpRequestException)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, "invalid_request",
                "The request body is missing or not valid JSON.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteAsync(context, StatusCodes.Status500InternalServerError, "server_error",
                "Something went wrong on the server. Please try again.");
        }
    }

    private static async Task WriteAsync(HttpContext context, int status, string code, string message)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { error = new { code, message } });
    }
}
