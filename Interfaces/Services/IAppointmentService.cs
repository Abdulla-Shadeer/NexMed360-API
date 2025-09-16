using FinalProjectApi.Common;
using FinalProjectApi.Dtos.Appointment;
using FinalProjectApi.Entities.Enums;

namespace FinalProjectApi.Interfaces.Services;

public interface IAppointmentService
{
    public Task<Result<ViewAppointmentDto>> Create(CreateAppointmentDto createAppointment, string requesterId);
    public Task<Result<List<ViewAppointmentDto>>> GetOwn(ViewAppointmentsFilterDto filter, string userId);
    public Task<Result<List<ViewAppointmentDto>>> GetHospitalAppointments(ViewAppointmentsFilterDto filter, string userId);
    public Task<Result<ViewAppointmentDto>> Modify(ModifyAppointmentDto modifyAppointmentDto, string requesterId);
    public Task<Result<ViewAppointmentDto>> Get(string appointmentId);
    public Task<Result<ViewAppointmentDto>> ChangeStatus(string appointmentId, AppointmentStatus status);
    public Task<Result<ViewAppointmentDto>> Cancel(CancellationRequestDto cancellationRequestDto, string requesterId);
    public Task<Result<ViewDoctorSessionDto>> CreateDoctorSession(CreateDoctorSessionDto dto);
    public Task<Result<List<ViewDoctorSessionDto>>> GetDoctorSessions(Guid doctorId);
    public Task<Result<List<ViewSessionSlotDto>>> GetSessionSlots(GetSessionSlotsDto dto);
}
