using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Interfaces.Services;

public sealed record AppointmentFilter(DateTime? FromDate, DateTime? ToDate, Guid? PatientId, Guid? PractitionerRoleId, Guid? OrganizationId, AppointmentStatus? Status);
public interface IAppointmentRepository
{
    Task<SessionSlot?> FindSlot(Guid id);
    Task<Appointment?> FindReadable(Guid id, string callerId, IReadOnlyCollection<string> permissions);
    Task<bool> TryBook(Appointment appointment, SessionSlot slot);
    Task<Appointment?> Find(Guid id);
    Task<CancellationRequest?> FindCancellationRequest(Guid appointmentId);
    Task AddCancellationRequest(CancellationRequest request);
    void RemoveCancellationRequest(CancellationRequest request);
    Task SaveChanges();
    Task<List<Appointment>> GetOwn(AppointmentFilter filter, string userId);
    Task<List<Appointment>> GetOrganizationAppointments(AppointmentFilter filter, string userId);
    Task<bool> PatientExists(Guid id);
    Task<bool> PractitionerRoleExists(Guid id);
    Task<bool> OrganizationExists(Guid id);
    Task<List<ViewAppointmentDto>> Views(IEnumerable<Appointment> rows);
    Task<ViewAppointmentDto?> View(Appointment? row);
}
