using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexMed360.Application.Patients;
using NexMed360Api.Data;
using NexMed360Api.Entities;

namespace NexMed360.Infrastructure.Repositories;

public sealed class RecordSharingRepository(ApplicationDbContext context, ILogger<RecordSharingRepository>? logger = null) : IRecordSharingRepository
{
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

    private Task<List<RecordSharingRequestDto>> Views(IQueryable<RecordSharingRequest> requests)
    {
        var now = DateTime.UtcNow;
        // The request scope is checked above; source profiles only provide display details.
        var query =
            from request in requests
            join patient in context.Patients.IgnoreQueryFilters() on request.PatientId equals patient.Id
            join patientPerson in context.People on patient.PersonId equals patientPerson.Id
            join role in context.PractitionerRoles.IgnoreQueryFilters() on request.PractitionerRoleId equals role.Id
            join practitioner in context.Practitioners on role.PractitionerId equals practitioner.Id
            join person in context.People on practitioner.PersonId equals person.Id
            join organization in context.Organizations on request.ReceivingOrganizationId equals organization.Id
            orderby request.CreatedDate descending, request.Id
            select new RecordSharingRequestDto
            {
                Id = request.Id, PatientId = request.PatientId, PatientName = patientPerson.FirstName + " " + patientPerson.LastName,
                ReceivingOrganizationId = organization.Id, OrganizationName = organization.Name, OrganizationContact = organization.Phone,
                PractitionerName = person.FirstName + " " + person.LastName, PractitionerRole = role.RoleName, PractitionerEmail = person.Email ?? "",
                Purpose = request.Purpose, RecordFromDate = request.RecordFromDate, RecordToDate = request.RecordToDate, ExpiresAt = DateTime.SpecifyKind(request.ExpiresAt, DateTimeKind.Utc),
                Status = request.ExpiresAt <= now && (request.Status == RecordSharingStatus.Pending || request.Status == RecordSharingStatus.Approved)
                    ? "Expired" : request.Status.ToString(),
                RequestedAt = request.CreatedDate.HasValue ? DateTime.SpecifyKind(request.CreatedDate.Value, DateTimeKind.Utc) : null,
                ApprovedAt = request.ApprovedAt.HasValue ? DateTime.SpecifyKind(request.ApprovedAt.Value, DateTimeKind.Utc) : null,
                DecidedAt = request.DecidedAt.HasValue ? DateTime.SpecifyKind(request.DecidedAt.Value, DateTimeKind.Utc) : null,
                RevokedAt = request.RevokedAt.HasValue ? DateTime.SpecifyKind(request.RevokedAt.Value, DateTimeKind.Utc) : null
            };
        return query.ToListAsync();
    }

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
