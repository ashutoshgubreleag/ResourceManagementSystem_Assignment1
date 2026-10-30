using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ApexPost.Utilities.Filters;

/// <summary>
/// On an invalid model, redirect back to <see cref="Action"/> (with the errors in TempData) instead
/// of redisplaying a view. Used by the basket screen, where several small forms post to different
/// actions but all errors belong on the same page (Post/Redirect/Get even on failure).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RedirectOnInvalidModelAttribute(string action) : Attribute
{
    public string Action { get; } = action;
}

/// <summary>
/// Consistent handling of invalid model state, so actions don't each repeat
/// <c>if (!ModelState.IsValid) return View(model);</c>:
/// <list type="bullet">
/// <item>API / JSON callers and GET requests (bad query string): 400 with ValidationProblemDetails.</item>
/// <item>Actions with <see cref="RedirectOnInvalidModelAttribute"/>: errors to TempData, redirect.</item>
/// <item>Other form posts: redisplay the action's view with the posted model and its errors.</item>
/// </list>
/// A filter (not middleware) because model binding and ModelState only exist inside MVC.
/// </summary>
public sealed class ValidationFilter(ITempDataDictionaryFactory tempDataFactory) : IAsyncActionFilter
{
    public const string ErrorsTempDataKey = "ValidationErrors";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.ModelState.IsValid)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var http = context.HttpContext;
        var isApi = DomainExceptionMapper.IsApiRequest(context.ActionDescriptor, http);

        if (isApi || HttpMethods.IsGet(http.Request.Method))
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
            });
            return;
        }

        var redirect = context.ActionDescriptor.EndpointMetadata.OfType<RedirectOnInvalidModelAttribute>().FirstOrDefault();
        if (redirect is not null)
        {
            var messages = context.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => m.Length > 0).ToArray();
            tempDataFactory.GetTempData(http)[ErrorsTempDataKey] = string.Join("\n", messages);
            context.Result = new RedirectToActionResult(redirect.Action, null, new { id = context.RouteData.Values["id"] });
            return;
        }

        if (context.Controller is Controller controller)
        {
            var model = context.ActionArguments.Values.FirstOrDefault(v => v is not null && !v.GetType().IsPrimitive && v is not string);
            context.Result = controller.View(model);
            return;
        }

        context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
    }
}
