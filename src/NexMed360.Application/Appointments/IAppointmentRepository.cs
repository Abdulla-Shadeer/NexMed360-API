using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Interfaces.Services;

public sealed record AppointmentFilter(DateTime? FromDate, DateTime? ToDate, Guid? PatientId, Guid? DoctorId, Guid? HospitalId, AppointmentStatus? Status);
public interface IAppointmentRepository
{
    Task<SessionSlot?> FindSlot(Guid id);
    Task<Appointment?> FindReadable(Guid id, string callerId, IReadOnlyCollection<string> roles);
    Task<bool> TryBook(Appointment appointment, SessionSlot slot);
    Task<Appointment?> Find(Guid id);
    Task<CancellationRequest?> FindCancellationRequest(Guid appointmentId);
    Task AddCancellationRequest(CancellationRequest request);
    void RemoveCancellationRequest(CancellationRequest request);
    Task SaveChanges();
    Task<List<Appointment>> GetOwn(AppointmentFilter filter, string userId);
    Task<List<Appointment>> GetHospitalAppointments(AppointmentFilter filter, string userId);
    Task<bool> PatientExists(Guid id);
    Task<bool> DoctorExists(Guid id);
    Task<bool> HospitalExists(Guid id);
}
