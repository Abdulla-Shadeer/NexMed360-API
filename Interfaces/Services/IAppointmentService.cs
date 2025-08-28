using FinalProjectApi.Common;
using FinalProjectApi.Dtos.Appointment;

namespace FinalProjectApi.Interfaces.Services;

public interface IAppointmentService
{
    public Task<Result<ViewAppointmentDto>> CreateAppointment(CreateAppointmentDto createAppointment, string requesterId);

    public Task<Result<List<ViewAppointmentDto>>> ViewOwnAppointments(ViewAppointmentsFilterDto filter, string userId);
    public Task<Result<List<ViewAppointmentDto>>> ViewHospitalAppointments(ViewAppointmentsFilterDto filter, string userId);

    public Task<Result<ViewAppointmentDto>> ModifyAppointment(ModifyAppointmentDto modifyAppointmentDto, string requesterId);

    public Task<Result<ViewAppointmentDto>> ViewAppointment(string appointmentId);

    public Task<Result<ViewAppointmentDto>> ConfirmOrCompleteAppointment(string appointmentId, bool isComplete);

    public Task<Result<ViewAppointmentDto>> CancelAppointment(CancellationRequestDto cancellationRequestDto, string requesterId);
}
