using NexMed360Api.Common.Auth;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NexMed360.Application.Access;
using NexMed360Api.Common.ApplicationErrors;

namespace NexMed360Api.Common;

[AttributeUsage(AttributeTargets.Method)]
public sealed class WorkspaceAttribute(string modes, string? permission = null, bool assignment = false) : Attribute
{
    public string[] Modes { get; } = modes.Split(',');
    public string? Permission { get; } = permission;
    public bool Assignment { get; } = assignment;
}

public sealed class WorkspaceFilter(WorkspaceService service, WorkspaceAccess access) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) { await next(); return; }

        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) { context.Result = new UnauthorizedResult(); return; }

        var requiredAttribute = context.ActionDescriptor.EndpointMetadata.OfType<WorkspaceAttribute>().SingleOrDefault();
        if (requiredAttribute != null)
        {
            var key = context.HttpContext.Request.Headers["X-Workspace"].ToString();
            var workspace = await service.Resolve(userId, key);

            if (workspace == null || !requiredAttribute.Modes.Contains(workspace.Mode)
                || workspace.Mode == WorkspaceModes.Work && requiredAttribute.Permission != null && !requiredAttribute.Permission.Split(',').Any(workspace.Permissions.Contains)
                || requiredAttribute.Assignment && !workspace.PractitionerRoleId.HasValue)
            {
                context.Result = new ObjectResult(Result<object>.Failure(ErrorCode.SYS_Unauthorized,
                    "You do not have access in this workspace. Choose another workspace or ask your administrator.", 403))
                { StatusCode = 403 };
                return;
            }

            access.Select(workspace);
        }

        await next();
    }
}
