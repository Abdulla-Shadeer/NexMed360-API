using System.Security.Claims;
using NexMed360Api.Common.Auth;

namespace NexMed360Api.Common;

internal static class HttpCallerContext
{
    public static CallerContext ToCallerContext(this ClaimsPrincipal user) => new(
        user.FindFirstValue(ClaimTypes.NameIdentifier),
        new[] { UserRoles.SUPERUSER, UserRoles.HOSPITAL_ADMIN, UserRoles.RECEPTIONIST, UserRoles.DOCTOR, UserRoles.PATIENT }
            .Where(user.IsInRole).ToArray());
}
