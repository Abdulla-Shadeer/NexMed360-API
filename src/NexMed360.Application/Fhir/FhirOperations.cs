namespace NexMed360.Application.Fhir;

public static class FhirOperations
{
    public const string PatientsSearch = "patients.search";
    public const string RecordsRead = "records.read";
    public const string SharingRequest = "sharing.request";
    public static readonly IReadOnlyList<string> All = Array.AsReadOnly(new[] { PatientsSearch, RecordsRead, SharingRequest });
}
