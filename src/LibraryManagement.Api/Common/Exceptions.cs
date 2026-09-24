namespace LibraryManagement.Api.Common;

/// <summary>Base type for errors the API reports to clients as a specific HTTP status.</summary>
public abstract class AppException(string message, int statusCode, string title) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
}

public class NotFoundException(string message)
    : AppException(message, StatusCodes.Status404NotFound, "Resource not found");

/// <summary>The request is well-formed but breaks a library rule (no copies left, duplicate reservation, ...).</summary>
public class BusinessRuleException(string message)
    : AppException(message, StatusCodes.Status409Conflict, "Business rule violation");

public class ForbiddenException(string message)
    : AppException(message, StatusCodes.Status403Forbidden, "Forbidden");

public class UnauthorizedException(string message)
    : AppException(message, StatusCodes.Status401Unauthorized, "Unauthorized");
