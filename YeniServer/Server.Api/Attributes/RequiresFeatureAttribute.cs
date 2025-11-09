using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Server.Domain.Enums;
using Server.Infrastructure.Data;

namespace Server.Api.Attributes;

/// <summary>
/// Requires specific enterprise feature to be enabled in license
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequiresFeatureAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly EnterpriseFeature _requiredFeature;

    public RequiresFeatureAttribute(EnterpriseFeature requiredFeature)
    {
        _requiredFeature = requiredFeature;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Get database context from services
        var dbContext = context.HttpContext.RequestServices.GetService<ApplicationDbContext>();
        if (dbContext == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        // Get active license
        var license = await dbContext.Licenses
            .Where(l => l.IsActive && l.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(l => l.IssuedAt)
            .FirstOrDefaultAsync();

        if (license == null)
        {
            context.Result = new ObjectResult(new 
            { 
                error = "No active license found", 
                code = "LICENSE_REQUIRED",
                requiredFeature = _requiredFeature.ToString()
            })
            {
                StatusCode = 403
            };
            return;
        }

        // Check if license has required feature
        if (!license.Features.HasFlag(_requiredFeature))
        {
            context.Result = new ObjectResult(new 
            { 
                error = $"Enterprise feature '{_requiredFeature}' not enabled in license", 
                code = "FEATURE_NOT_LICENSED",
                requiredFeature = _requiredFeature.ToString(),
                currentEdition = license.Edition.ToString(),
                upgradeRequired = true
            })
            {
                StatusCode = 403
            };
            return;
        }

        // Store license info in HttpContext
        context.HttpContext.Items["LicenseId"] = license.Id;
        context.HttpContext.Items["LicenseEdition"] = license.Edition;
        context.HttpContext.Items["LicenseFeatures"] = license.Features;
    }
}
