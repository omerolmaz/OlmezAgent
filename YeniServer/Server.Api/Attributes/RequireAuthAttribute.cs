using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Server.Api.Attributes;

/// <summary>
/// Requires a valid user token to access the endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAuthAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Check for Authorization header
        if (!context.HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            context.Result = new UnauthorizedObjectResult(new 
            { 
                error = "Authorization header missing", 
                code = "AUTH_REQUIRED" 
            });
            return;
        }

        var token = authHeader.ToString().Replace("Bearer ", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            context.Result = new UnauthorizedObjectResult(new 
            { 
                error = "Invalid authorization token", 
                code = "INVALID_TOKEN" 
            });
            return;
        }

        // Store token in HttpContext for services to use
        context.HttpContext.Items["UserToken"] = token;
        
        // Actual token validation will be done in services
        await Task.CompletedTask;
    }
}
