using NexMed360.Application.Patients;
using NexMed360.Application.Practitioners;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Entities;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.Application.Fhir;

public sealed class FhirSourceService(FhirPartnerAccess partner, IPatientRepository patients,
    IClinicalRecordRepository records, IRecordSharingRepository sharing, IOrganizationRepository organizations,
    IPractitionerRoleRepository roles, IPractitionerRepository practitioners) : IFhirSourceService
{
    public async Task<Result<List<PatientIdentityDto>>> SearchPatients(PatientIdentityFilter filter)
    {
        if (!partner.Has(FhirOperations.PatientsSearch)) return Denied();
        if (filter.Offset < 0 || filter.Offset > 10000 || filter.Count < 1 || filter.Count > 30)
            return BadRequest("Choose a page size from 1 to 30 and a valid offset.");
        var name = filter.Name?.Trim();
        var email = filter.Email?.Trim();
        if (!filter.PatientId.HasValue && string.IsNullOrEmpty(name) && string.IsNullOrEmpty(email))
            return BadRequest("Enter a patient name, email, or identifier.");
        if (name?.Length > 200 || email?.Length > 320 || filter.PatientId == Guid.Empty)
            return BadRequest("Check the patient search values.");
        return await patients.SearchIdentities(filter with { Name = name, Email = email });
    }

    public async Task<Result<PatientIdentityDto>> GetPatient(Guid id)
    {
        if (!partner.Has(FhirOperations.PatientsSearch)) return Denied();
        var patient = await patients.FindIdentity(id);
        if (patient == null) return NotFound();
        return patient;
    }

    public async Task<Result<List<ClinicalRecord>>> GetRecords(Guid patientId)
    {
        if (!partner.Has(FhirOperations.RecordsRead)) return Denied();
        var requests = await sharing.GetForPartner(partner.PartnerId!, [patientId]);
        var now = DateTime.UtcNow;
        if (!requests.Any(request => request.IsApprovedAt(now))) return ApprovalRequired();
        var rows = await records.GetForExchange(patientId, partner.LocalOrganizationId);
        var permitted = rows.Where(record => record.OrganizationId == partner.LocalOrganizationId
            && requests.Any(request => request.AllowsRecord(record, now))).ToList();
        foreach (var record in permitted) FilterPrescriptions(record, requests, now);
        return permitted;
    }

    public async Task<Result<ClinicalRecord>> GetRecord(Guid id)
    {
        if (!partner.Has(FhirOperations.RecordsRead)) return Denied();
        var record = await records.FindForExchange(id, partner.LocalOrganizationId);
        if (record == null || !await CanRead(record)) return NotFound();
        return record;
    }

    public async Task<Result<Prescription>> GetPrescription(Guid id)
    {
        if (!partner.Has(FhirOperations.RecordsRead)) return Denied();
        var record = await records.FindForPrescriptionExchange(id, partner.LocalOrganizationId);
        if (record == null || !await CanRead(record)) return NotFound();
        var prescription = record.Prescriptions?.FirstOrDefault(row => row.Id == id && row.OrganizationId == partner.LocalOrganizationId);
        if (prescription == null) return NotFound();
        return prescription;
    }

    public async Task<Result<Organization>> GetOrganization(Guid id)
    {
        if (!partner.Has(FhirOperations.RecordsRead)) return Denied();
        if (id != partner.LocalOrganizationId || !await CanReadAuthor()) return NotFound();
        var organization = await organizations.Find(id);
        if (organization == null) return NotFound();
        return organization;
    }

    public async Task<Result<PractitionerRole>> GetPractitionerRole(Guid id)
    {
        if (!partner.Has(FhirOperations.RecordsRead)) return Denied();
        if (!await CanReadAuthor(practitionerRoleId: id)) return NotFound();
        var role = await roles.Find(id);
        if (role == null || role.OrganizationId != partner.LocalOrganizationId) return NotFound();
        return role;
    }

    public async Task<Result<PractitionerProfileDto>> GetPractitioner(Guid id)
    {
        if (!partner.Has(FhirOperations.RecordsRead)) return Denied();
        if (!await CanReadAuthor(practitionerId: id)) return NotFound();
        var practitioner = await practitioners.FindProfile(id);
        if (practitioner == null) return NotFound();
        return practitioner;
    }

    // Checks current patient consent for this source record and removes prescriptions outside that consent.
    private async Task<bool> CanRead(ClinicalRecord record)
    {
        if (record.OrganizationId != partner.LocalOrganizationId) return false;
        var requests = await sharing.GetForPartner(partner.PartnerId!, [record.PatientId]);
        var now = DateTime.UtcNow;
        if (!requests.Any(request => request.AllowsRecord(record, now))) return false;
        FilterPrescriptions(record, requests, now);
        return true;
    }

    // Keeps only approved prescriptions; prescriptions added after approval are excluded.
    private static void FilterPrescriptions(ClinicalRecord record, List<RecordSharingRequest> requests, DateTime now)
    {
        record.Prescriptions = record.Prescriptions?.Where(prescription =>
            requests.Any(request => request.AllowsPrescription(prescription, record, now))).ToList() ?? [];
    }

    // Allows organization, practitioner or role details only when linked to a record this partner can read.
    // Without a practitioner or role filter, checks for any readable record from the source organization.
    private async Task<bool> CanReadAuthor(Guid? practitionerRoleId = null, Guid? practitionerId = null)
    {
        var rows = await records.GetForAuthorExchange(partner.PartnerId!, partner.LocalOrganizationId, practitionerRoleId, practitionerId);
        if (rows.Count == 0) return false;
        var requests = await sharing.GetForPartner(partner.PartnerId!, rows.Select(row => row.PatientId).Distinct().ToArray());
        var now = DateTime.UtcNow;
        return rows.Any(record => record.OrganizationId == partner.LocalOrganizationId
            && requests.Any(request => request.AllowsRecord(record, now)));
    }

    private static ApiError Denied() => ApiError.From(ErrorCode.SYS_Unauthorized, "This partner cannot perform this operation.", 403);
    private static ApiError ApprovalRequired() => ApiError.From(ErrorCode.SYS_Unauthorized, "Patient approval is required for these records.", 403);
    private static ApiError NotFound() => ApiError.From(ErrorCode.SYS_BadRequest, "The requested resource was not found.", 404);
    private static ApiError BadRequest(string message) => ApiError.From(ErrorCode.SYS_BadRequest, message);
}
