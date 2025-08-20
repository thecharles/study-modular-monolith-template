using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Modules.Carriers.Features.Features.Shared.Routes;

namespace Modules.Carriers.Features.Tracing;

public class CarriersTracingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(RouteConsts.BaseRoute, StringComparison.Ordinal))
        {
            await next(context);
            return;
        }

        var operationName = GetOperationName(context);
        using var activity = CarriersActivitySource.Instance.StartActivity($"{CarriersActivitySource.Instance.Name}.{operationName}");

        activity?.SetTag("module", CarriersActivitySource.Instance.Name);
        activity?.SetTag("http.method", context.Request.Method);
        activity?.SetTag("http.path", context.Request.Path);
        activity?.SetTag("operation", operationName);

        try
        {
            await next(context);

            activity?.SetTag("http.status_code", context.Response.StatusCode);

            activity?.SetStatus(context.Response.StatusCode >= 400
                ? ActivityStatusCode.Error
                : ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.message", ex.Message);
            throw;
        }
    }

    private static string GetOperationName(HttpContext context)
    {
        var method = context.Request.Method.ToUpper();

        var methodMapping = method switch
        {
            "POST" => "create",
            "GET" => "get",
            "PUT" => "update",
            "PATCH" => "update",
            "DELETE" => "delete",
            _ => method.ToLower()
        };

        return $"{CarriersActivitySource.Instance.Name}.{methodMapping}";
    }
}
