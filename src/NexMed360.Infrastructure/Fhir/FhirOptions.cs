namespace NexMed360.Infrastructure.Fhir;

public sealed class FhirOptions
{
    public const string SectionName = "Fhir";
    public bool Enabled { get; set; }
    public string PublicBaseUrl { get; set; } = "";
    public string IdentifierSystem { get; set; } = "";
    public List<FhirPartnerOptions> Partners { get; set; } = [];
}

public sealed class FhirPartnerOptions
{
    public string Id { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public string Contact { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public Guid LocalOrganizationId { get; set; }
    public Guid RemoteOrganizationId { get; set; }
    public List<string> AllowedOperations { get; set; } = [];
    public string IncomingApiKeyHash { get; set; } = "";
    public string OutgoingApiKeySecretName { get; set; } = "";
}
