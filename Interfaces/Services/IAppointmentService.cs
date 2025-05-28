using FinalProjectApi.Dtos.Appointment;

namespace FinalProjectApi.Interfaces.Services;

public interface IAppointmentService
{
    public Task<string> CreateAppointment(CreateAppointment createAppointment);
}
