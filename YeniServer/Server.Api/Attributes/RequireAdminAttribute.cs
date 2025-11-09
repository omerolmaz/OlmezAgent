using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Server.Domain.Enums;
using Server.Infrastructure.Data;

namespace Server.Api.Attributes;

/// <summary>
/// Requires admin role to access the endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAdminAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Check for Authorization header
        if (!context.HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            context.Result = new UnauthorizedObjectResult(new 
            { 
                error = "Authorization header missing", 
                code = "AUTH_REQUIRED" 
            });
            return Task.CompletedTask;
        }

        var token = authHeader.ToString().Replace("Bearer ", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            context.Result = new UnauthorizedObjectResult(new 
            { 
                error = "Invalid authorization token", 
                code = "INVALID_TOKEN" 
            });
            return Task.CompletedTask;
        }

        // Get database context from services
        var dbContext = context.HttpContext.RequestServices.GetService<ApplicationDbContext>();
        if (dbContext == null)
        {
            context.Result = new StatusCodeResult(500);
            return Task.CompletedTask;
        }

        // For now, use simplified auth - check if "admin" token exists in request
        // TODO: Implement proper JWT/Session based auth
        if (token != "admin_token_temp")
        {
            context.Result = new UnauthorizedObjectResult(new 
            { 
                error = "Admin authentication required", 
                code = "ADMIN_AUTH_REQUIRED",
                message = "Use 'Authorization: Bearer admin_token_temp' for testing" 
            });
            return Task.CompletedTask;
        }

        // Store auth info in HttpContext
        context.HttpContext.Items["IsAdmin"] = true;
        context.HttpContext.Items["UserToken"] = token;
        
        return Task.CompletedTask;
    }
}
