using FluentValidation;

namespace Cuadre.Application.Services;

internal static class ValidatorExtensions
{
    /// <summary>
    /// Explicit ValidateAsync + throw (instead of ValidateAndThrowAsync) keeps mocked IValidator&lt;T&gt; tests simple.
    /// ValidationMappingMiddleware turns the exception into a 400 with per-field errors.
    /// </summary>
    public static async Task ValidateOrThrowAsync<T>(this IValidator<T> validator, T instance,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(instance, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }
    }
}
