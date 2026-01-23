using NexMed360Api.Common;
using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Entities.Enums;

namespace NexMed360Api.Interfaces.Services;

public interface IAppointmentService
{
    public Task<Result<ViewAppointmentDto>> Create(CreateAppointmentDto createAppointment, string requesterId);
    public Task<Result<List<ViewAppointmentDto>>> GetOwn(ViewAppointmentsFilterDto filter, string userId);
    public Task<Result<List<ViewAppointmentDto>>> GetHospitalAppointments(ViewAppointmentsFilterDto filter, string userId);
    public Task<Result<ViewAppointmentDto>> Get(string appointmentId);
    public Task<Result<ViewAppointmentDto>> ChangeStatus(string appointmentId, AppointmentStatus status);
    public Task<Result<ViewAppointmentDto>> Cancel(CancellationRequestDto cancellationRequestDto, string requesterId);
}
