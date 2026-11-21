using NexMed360.Application.Patients;
using NexMed360.Application.Practitioners;
using NexMed360Api.Common;
using NexMed360Api.Entities;

namespace NexMed360.Application.Fhir;

public interface IFhirSourceService
{
    Task<Result<List<PatientIdentityDto>>> SearchPatients(PatientIdentityFilter filter);
    Task<Result<PatientIdentityDto>> GetPatient(Guid id);
    Task<Result<List<ClinicalRecord>>> GetRecords(Guid patientId);
    Task<Result<ClinicalRecord>> GetRecord(Guid id);
    Task<Result<Prescription>> GetPrescription(Guid id);
    Task<Result<Organization>> GetOrganization(Guid id);
    Task<Result<PractitionerRole>> GetPractitionerRole(Guid id);
    Task<Result<PractitionerProfileDto>> GetPractitioner(Guid id);
}
