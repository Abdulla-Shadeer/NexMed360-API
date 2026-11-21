namespace NexMed360.Application.Fhir;

// Only successful partner authentication populates this request-scoped identity.
public sealed class FhirPartnerAccess
{
    public string? PartnerId { get; private set; }
    public Guid LocalOrganizationId { get; private set; }
    public Guid RemoteOrganizationId { get; private set; }
    private string[] operations = [];
    public bool IsAuthenticated => PartnerId != null;
    public bool Has(string operation) => IsAuthenticated && operations.Contains(operation);

    public void Authenticate(string partnerId, Guid localOrganizationId, Guid remoteOrganizationId, IEnumerable<string> allowedOperations)
    {
        if (string.IsNullOrWhiteSpace(partnerId) || localOrganizationId == Guid.Empty || remoteOrganizationId == Guid.Empty)
            throw new ArgumentException("Partner identity and organization IDs are required.");
        PartnerId = partnerId;
        LocalOrganizationId = localOrganizationId;
        RemoteOrganizationId = remoteOrganizationId;
        operations = allowedOperations.Distinct().ToArray();
    }
}
