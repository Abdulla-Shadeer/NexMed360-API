using FinalProjectApi.Common;
using FinalProjectApi.Dtos.Appointment;

namespace FinalProjectApi.Interfaces.Services;

public interface IAppointmentService
{
    public Task<Result<string>> CreateAppointment(CreateAppointment createAppointment);

    public Task<Result<List<ViewAppointment>>> ViewAppointments(ViewAppointmentsFilter filter);

    public Task<Result<ViewAppointment>> ModifyAppointment(ModifyAppointmentDto modifyAppointmentDto);

    public Task<Result<ViewAppointment>> ViewAppointment(string appointmentId);
}
