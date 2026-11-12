using Cuadre.Application.Errors;
using FluentResults;

namespace Cuadre.Api.Mapping;

/// <summary>
/// Converts FluentResults from the Application layer into HTTP results.
/// Failures become RFC 7807 ProblemDetails using the status code stored in the error metadata.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToOkResult<T, TResponse>(this Result<T> result, Func<T, TResponse> map)
    {
        return result.IsSuccess
            ? TypedResults.Ok(map(result.Value))
            : result.Errors.ToProblem();
    }

    public static IResult ToCreatedAtRouteResult<T, TResponse>(
        this Result<T> result,
        Func<T, TResponse> map,
        string routeName,
        Func<T, object> routeValues)
    {
        return result.IsSuccess
            ? TypedResults.CreatedAtRoute(map(result.Value), routeName, routeValues(result.Value))
            : result.Errors.ToProblem();
    }

    public static IResult ToNoContentResult(this Result result)
    {
        return result.IsSuccess
            ? TypedResults.NoContent()
            : result.Errors.ToProblem();
    }

    private static IResult ToProblem(this IReadOnlyCollection<IError> errors)
    {
        var first = errors.First();
        var statusCode = first.Metadata.TryGetValue(ErrorMetadata.StatusCode, out var status)
            ? (int)status
            : StatusCodes.Status400BadRequest;

        return Results.Problem(
            statusCode: statusCode,
            title: first.Message,
            extensions: errors.Count > 1
                ? new Dictionary<string, object?> { ["errors"] = errors.Select(e => e.Message).ToArray() }
                : null);
    }
}
