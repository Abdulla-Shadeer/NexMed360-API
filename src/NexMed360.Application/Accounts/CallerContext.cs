namespace NexMed360Api.Common.Auth;

public sealed record CallerContext(string? UserId, IReadOnlyCollection<string> Permissions);
