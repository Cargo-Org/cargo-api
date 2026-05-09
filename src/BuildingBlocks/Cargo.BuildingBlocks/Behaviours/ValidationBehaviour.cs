using ErrorOr;
using FluentValidation;
using MediatR;

namespace Cargo.BuildingBlocks.Behaviours;

public sealed class ValidationBehaviour<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IErrorOr
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        // Run all validators in parallel — significant throughput improvement
        // when a command has multiple registered validators.
        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .Select(f => Error.Validation(
                code: f.PropertyName,
                description: f.ErrorMessage))
            .ToList();

        if (failures.Count != 0)
        {
            // (TResponse)(object) is preferred over (dynamic) — fails with a clear
            // InvalidCastException if misconfigured, rather than a dynamic dispatch error.
            // Works because ErrorOr<T> has an implicit conversion from List<Error>.
            var conversionMethod = typeof(TResponse)
                    .GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                    .FirstOrDefault(m =>
                        m.Name == "op_Implicit" &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(List<Error>))
                    ?? throw new InvalidOperationException(
                        $"Type {typeof(TResponse).Name} does not have an implicit conversion from List<Error>. " +
                        $"Ensure TResponse is ErrorOr<T>.");

            return (TResponse)conversionMethod.Invoke(null, [failures])!;
        }

        return await next(cancellationToken);
    }
}