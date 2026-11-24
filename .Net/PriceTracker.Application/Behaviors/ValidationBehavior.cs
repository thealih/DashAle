using FluentValidation;
using MediatR;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (result.Errors.FirstOrDefault() is { } failure)
                throw new PanelException(failure.CustomState is int status ? status : 422, failure.ErrorMessage);
        }

        return await next();
    }
}
