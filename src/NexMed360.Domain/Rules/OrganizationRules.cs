namespace NexMed360.Domain.Rules;

public static class OrganizationRules
{
    public static void ValidateOwnership(Guid organizationId, Guid? originalOrganizationId = null)
    {
        if (organizationId == Guid.Empty)
            throw new InvalidOperationException("Choose an organization before saving this record.");

        if (originalOrganizationId.HasValue && originalOrganizationId.Value != organizationId)
            throw new InvalidOperationException("A saved record cannot be moved to another organization.");
    }
}
