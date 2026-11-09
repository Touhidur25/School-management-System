namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// An expected, user-facing failure. Turned into a JSON error body by
/// <see cref="ApiExceptionMiddleware"/>.
/// </summary>
public class ApiException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
