using Microsoft.AspNetCore.Mvc.Filters;

namespace FeedbackHub.API.Common;

/// <summary>
/// Keeps the action's responses out of caches, referrers and search indexes. Set before model binding, so a validation
/// 400 carries the headers as well as the action's own answers.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PrivateLinkResponseAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        IHeaderDictionary headers = context.HttpContext.Response.Headers;
        headers.CacheControl = "no-store";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Robots-Tag"] = "noindex";
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
