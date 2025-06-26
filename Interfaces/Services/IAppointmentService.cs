using FinalProjectApi.Common;
using FinalProjectApi.Dtos.Appointment;

namespace FinalProjectApi.Interfaces.Services;

public interface IAppointmentService
{
    public Task<Result<ViewAppointmentDto>> CreateAppointment(CreateAppointmentDto createAppointment);

    public Task<Result<List<ViewAppointmentDto>>> ViewAppointments(ViewAppointmentsFilterDto filter);

    public Task<Result<ViewAppointmentDto>> ModifyAppointment(ModifyAppointmentDto modifyAppointmentDto);

    public Task<Result<ViewAppointmentDto>> ViewAppointment(string appointmentId);

    public Task<Result<ViewAppointmentDto>> ConfirmAppointment(string appointmentId);

    public Task<Result<ViewAppointmentDto>> CancelAppointment(CancellationRequestDto cancellationRequestDto);
}
