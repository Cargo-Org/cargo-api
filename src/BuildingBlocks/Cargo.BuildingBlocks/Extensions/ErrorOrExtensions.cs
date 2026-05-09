using ErrorOr;
using Microsoft.AspNetCore.Http;

namespace Cargo.BuildingBlocks.Extensions;

public static class ErrorOrExtensions
{
    /// <summary>
    /// Converts a list of ErrorOr errors to an RFC 9457 ProblemDetails HTTP result.
    /// Validation errors produce HTTP 400 with a structured errors dictionary.
    /// All other error types map to their corresponding HTTP status code.
    /// </summary>
    public static IResult ToProblemResult(this List<Error> errors)
    {
        if (errors.Count == 0)
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);

        // If all errors are validation errors, use ValidationProblem for structured
        // field-level error reporting. Mobile clients can map these directly to form fields.
        if (errors.All(e => e.Type == ErrorType.Validation))
        {
            return Results.ValidationProblem(
                errors.GroupBy(e => e.Code)
                      .ToDictionary(
                          group => group.Key,
                          group => group.Select(e => e.Description).ToArray()
                      )
            );
        }

        // For mixed or non-validation errors, use the first error to determine status.
        var first = errors[0];

        var statusCode = first.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Problem(
            statusCode: statusCode,
            title: first.Code,
            detail: first.Description
        );
    }
}