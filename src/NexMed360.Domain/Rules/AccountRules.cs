namespace NexMed360.Domain.Rules;

public static class AccountRules
{
    public static bool IsSelf(string? targetId, string callerId)
        => string.Equals(targetId, callerId, StringComparison.Ordinal);
}
