using FinalProjectApi.Common;
using FinalProjectApi.Common.Exceptions;
using FinalProjectApi.Data;
using FinalProjectApi.Dtos.Appointment;
using FinalProjectApi.Entities;
using FinalProjectApi.Interfaces.Services;

namespace FinalProjectApi.Services;

public class AppointmentService : IAppointmentService
{
    private readonly ApplicationDbContext _context;

    public AppointmentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> CreateAppointment(CreateAppointment createAppointment)
    {
        var doctorId = Guid.Parse(createAppointment.DoctorId);
        var patientId = Guid.Parse(createAppointment.PatientId);
        var isDoctorAvailable = await AppointmentHelper.IsTimeSlotAvailableAsync
            (_context, doctorId, createAppointment.AppointmentDateTime, createAppointment.Duration, true);
        
        if (!isDoctorAvailable) 
            throw new BadRequestException("Doctor is not available at the requested time!");

        var isPatientAvailable = await AppointmentHelper.IsTimeSlotAvailableAsync
            (_context, patientId, createAppointment.AppointmentDateTime, createAppointment.Duration, false);

        if (!isPatientAvailable)
            throw new BadRequestException("Patient is not available at the requested time!");

        // Both doctor and patient is available at the requested time
        var appointment = new Appointment
        {
            DoctorId = doctorId,
            PatientId = patientId,
            AppointmentDateTime = createAppointment.AppointmentDateTime,
            Duration = createAppointment.Duration,
            Remarks = createAppointment.Remarks,
            Status = Entities.Enums.AppointmentStatus.Pending,
            Location = createAppointment.Location,
        };

        var appointmentRef = (await _context.Appointments.AddAsync(appointment)).Entity.Id;
        await _context.SaveChangesAsync();
        return appointmentRef.ToString();
    }
}
