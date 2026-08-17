using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MyAssistant.Application.Common;

namespace MyAssistant.API.Middleware;

public class FluentValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var (name, argument) in context.ActionArguments)
        {
            if (argument is null) continue;
            var type = argument.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(type);
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator) continue;

            var validateAsync = validatorType.GetMethod(nameof(IValidator<int>.ValidateAsync), new[] { type, typeof(CancellationToken) });
            if (validateAsync is null) continue;
            var task = validateAsync.Invoke(validator, new[] { argument, CancellationToken.None }) as Task<ValidationResult>;
            if (task is null) continue;
            var result = await task.ConfigureAwait(false);
            if (result.IsValid) continue;

            context.ModelState.Clear();
            foreach (var error in result.Errors)
            {
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            context.Result = new BadRequestObjectResult(ApiResponse<object?>.Fail(
                "Validation failed.",
                result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList()));
            return;
        }
        await next();
    }
}