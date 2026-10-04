namespace Phichat.Application.Common.Exceptions;

/// <summary>
/// Base type for expected failures. The API turns these into ProblemDetails
/// responses with <see cref="StatusCode"/>; anything else is reported as 500.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(int statusCode, string code, string message) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }

    /// <summary>Stable machine-readable error code for clients (e.g. "invalid_credentials").</summary>
    public string Code { get; }
}

public sealed class BadRequestException : AppException
{
    public BadRequestException(string code, string message) : base(400, code, message) { }
}

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string code, string message) : base(401, code, message) { }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string code, string message) : base(403, code, message) { }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string code, string message) : base(404, code, message) { }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string code, string message) : base(409, code, message) { }
}

public sealed class TooManyRequestsException : AppException
{
    public TooManyRequestsException(string code, string message, TimeSpan? retryAfter = null)
        : base(429, code, message)
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan? RetryAfter { get; }
}
