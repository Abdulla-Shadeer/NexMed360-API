namespace NexMed360Api.Common.Auth;

public sealed record CallerContext(string? UserId, IReadOnlyCollection<string> Roles)
{
    public bool IsInRole(string role) => Roles.Contains(role);
}
