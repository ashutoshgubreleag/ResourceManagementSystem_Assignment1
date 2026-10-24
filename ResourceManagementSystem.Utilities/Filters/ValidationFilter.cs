using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ResourceManagementSystem.Utilities;

public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            var acceptHeaders = context.HttpContext.Request.Headers.Accept;
            var acceptsJson = acceptHeaders.Count > 0 && acceptHeaders.Any(a =>
                !string.IsNullOrWhiteSpace(a) &&
                a.Contains("application/json", StringComparison.OrdinalIgnoreCase));

            if (context.HttpContext.Request.Method == HttpMethods.Get ||
                acceptsJson ||
                context.HttpContext.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                context.Result = new BadRequestObjectResult(context.ModelState);
                return;
            }

            var controller = context.Controller as Controller;
            var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var viewName = actionDescriptor?.ActionName ?? "Index";
            var model = context.ActionArguments.Count > 0 ? context.ActionArguments.First().Value : controller?.ViewData.Model;

            if (controller is not null)
            {
                controller.ViewData.Model = model;
            }

            context.Result = new ViewResult { ViewName = viewName };
            return;
        }

        await next();
    }
}
