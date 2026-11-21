using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexMed360.Infrastructure.Fhir;
using NexMed360.Application.Patients;
using NexMed360Api.Data;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Repositories;

public sealed class RecordSharingRepository(ApplicationDbContext context, ILogger<RecordSharingRepository>? logger = null,
    IOptionsSnapshot<FhirOptions>? settings = null) : IRecordSharingRepository
{
    public Task<List<RecordSharingRequest>> GetForPartner(string partnerId, IReadOnlyCollection<Guid> patientIds) =>
        context.RecordSharingRequests.AsNoTracking().Where(request => request.ReceivingPartnerId == partnerId
            && request.ReceivingOrganizationId == null && request.PractitionerRoleId == null
            && patientIds.Contains(request.PatientId)).ToListAsync();

    public async Task Add(RecordSharingRequest request)
    {
        context.RecordSharingRequests.Add(request);
        await context.SaveChangesAsync();
        logger?.LogInformation("Record sharing request {RequestId} created for patient {PatientId} and organization {OrganizationId}.",
            request.Id, request.PatientId, request.ReceivingOrganizationId);
    }

    public Task<bool> PatientHasPortalAccess(Guid patientId) => context.Patients.AnyAsync(patient => patient.Id == patientId
        && context.Users.Any(user => user.PersonId == patient.PersonId && user.IsActive));

    public Task<RecordSharingRequest?> FindForPatient(Guid id, Guid patientId) => context.RecordSharingRequests
        .FirstOrDefaultAsync(row => row.Id == id && row.PatientId == patientId);

    public Task<List<RecordSharingRequestDto>> ForPatient(Guid patientId) => Views(
        context.RecordSharingRequests.Where(row => row.PatientId == patientId));

    public Task<List<RecordSharingRequestDto>> ForOrganization(Guid organizationId, Guid? patientId) => Views(
        context.RecordSharingRequests.Where(row => row.ReceivingOrganizationId == organizationId
            && (!patientId.HasValue || row.PatientId == patientId)
            && context.PractitionerRoles.Any(role => role.Id == row.PractitionerRoleId && role.OrganizationId == organizationId
                && (!context.Access.HasLocationScope || role.LocationId.HasValue && context.Access.LocationIds.Contains(role.LocationId.Value)))));

    private async Task<List<RecordSharingRequestDto>> Views(IQueryable<RecordSharingRequest> requests)
    {
        var rows = await requests.AsNoTracking().OrderByDescending(row => row.CreatedDate).ThenBy(row => row.Id).ToListAsync();
        if (rows.Count == 0) return [];

        // Scope was checked by the caller. These queries only load display details for those requests.
        var patientIds = rows.Select(row => row.PatientId).Distinct().ToArray();
        var patients = await (from patient in context.Patients.IgnoreQueryFilters()
            join person in context.People on patient.PersonId equals person.Id
            where patientIds.Contains(patient.Id)
            select new { patient.Id, Name = person.FirstName + " " + person.LastName }).ToDictionaryAsync(row => row.Id);

        var roleIds = rows.Where(row => row.PractitionerRoleId.HasValue).Select(row => row.PractitionerRoleId!.Value).Distinct().ToArray();
        var roles = await (from role in context.PractitionerRoles.IgnoreQueryFilters()
            join practitioner in context.Practitioners on role.PractitionerId equals practitioner.Id
            join person in context.People on practitioner.PersonId equals person.Id
            where roleIds.Contains(role.Id)
            select new { role.Id, role.RoleName, Name = person.FirstName + " " + person.LastName, person.Email }).ToDictionaryAsync(row => row.Id);
        var organizationIds = rows.Where(row => row.ReceivingOrganizationId.HasValue).Select(row => row.ReceivingOrganizationId!.Value).Distinct().ToArray();
        var organizations = await context.Organizations.Where(row => organizationIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        var now = DateTime.UtcNow;

        return rows.Select(request =>
        {
            var role = roles.GetValueOrDefault(request.PractitionerRoleId ?? Guid.Empty);
            var organization = organizations.GetValueOrDefault(request.ReceivingOrganizationId ?? Guid.Empty);
            var partner = settings?.Value.Partners.FirstOrDefault(row => row.Id == request.ReceivingPartnerId);
            var external = request.ReceivingPartnerId != null;
            return new RecordSharingRequestDto
            {
                Id = request.Id, PatientId = request.PatientId, PatientName = patients.GetValueOrDefault(request.PatientId)?.Name ?? "",
                ReceivingOrganizationId = request.ReceivingOrganizationId, ReceivingPartnerId = request.ReceivingPartnerId,
                OrganizationName = external ? partner?.Name ?? request.ReceivingPartnerId! : organization?.Name ?? "",
                OrganizationContact = external ? partner?.Contact ?? "" : organization?.Phone ?? "",
                PractitionerName = external ? request.RequesterName ?? "" : role?.Name ?? "",
                PractitionerRole = external ? request.RequesterRole ?? "" : role?.RoleName ?? "",
                PractitionerEmail = external ? request.RequesterContact ?? "" : role?.Email ?? "",
                Purpose = request.Purpose, RecordFromDate = request.RecordFromDate, RecordToDate = request.RecordToDate,
                ExpiresAt = DateTime.SpecifyKind(request.ExpiresAt, DateTimeKind.Utc),
                Status = request.ExpiresAt <= now && (request.Status == RecordSharingStatus.Pending || request.Status == RecordSharingStatus.Approved)
                    ? "Expired" : request.Status.ToString(),
                RequestedAt = Utc(request.CreatedDate), ApprovedAt = Utc(request.ApprovedAt),
                DecidedAt = Utc(request.DecidedAt), RevokedAt = Utc(request.RevokedAt)
            };
        }).ToList();
    }

    private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    public async Task<bool> Save(RecordSharingRequest request)
    {
        try
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("Record sharing request {RequestId} changed to {Status} by account {UserId}.", request.Id, request.Status, request.DecisionByUserId);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.Entry(request).State = EntityState.Detached;
            return false;
        }
    }
}
