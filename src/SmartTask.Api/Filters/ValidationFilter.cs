using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Common.Validation;

namespace SmartTask.Api.Filters;

/// <summary>
/// Global action filter (API-CONVENTIONS §4). For each action argument it resolves
/// <c>IValidator&lt;TArg&gt;</c>. When one exists and the argument fails validation, the request is
/// short-circuited with a 400 <see cref="BaseResponse"/> listing the errors. Arguments without a
/// validator are ignored.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                var response = BaseResponse.ValidationFailed(result.ToErrorDictionary());
                response.TraceId = context.HttpContext.TraceIdentifier;
                context.Result = new BadRequestObjectResult(response);
                return;
            }
        }

        await next();
    }
}
