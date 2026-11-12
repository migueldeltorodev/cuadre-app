using FluentResults;

namespace Cuadre.Application.Errors;

public static class ErrorMetadata
{
    public const string StatusCode = "StatusCode";
}

// Expected business failures. Input-shape validation is NOT modelled here: it throws
// FluentValidation.ValidationException and ValidationMappingMiddleware returns a 400 with per-field errors.
// Status codes are plain ints so Application stays free of ASP.NET dependencies.

public class NotFoundError : Error
{
    public NotFoundError(string message) : base(message)
    {
        Metadata.Add(ErrorMetadata.StatusCode, 404);
    }

    public static NotFoundError For<T>(Guid id) => new($"{typeof(T).Name} with id '{id}' was not found");
}

public class ConflictError : Error
{
    public ConflictError(string message) : base(message)
    {
        Metadata.Add(ErrorMetadata.StatusCode, 409);
    }
}

/// <summary>A business rule (RN-xx in docs/REQUISITOS_APP.md) was violated. Maps to 422.</summary>
public class BusinessRuleError : Error
{
    public BusinessRuleError(string message) : base(message)
    {
        Metadata.Add(ErrorMetadata.StatusCode, 422);
    }
}
