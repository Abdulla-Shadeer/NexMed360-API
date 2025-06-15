using AutoMapper;
using FinalProjectApi.Common;
using FinalProjectApi.Common.Exceptions;
using FinalProjectApi.Data;
using FinalProjectApi.Dtos.Appointment;
using FinalProjectApi.Entities;
using FinalProjectApi.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FinalProjectApi.Services;

public class AppointmentService : IAppointmentService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public AppointmentService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
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

    public async Task<List<ViewAppointment>> ViewAppointments(ViewAppointmentsFilter filter)
    {
        if (filter == null || (filter.PatientId.IsNullOrEmpty() && filter.DoctorId.IsNullOrEmpty())) { 
            throw new BadRequestException("At least one person ID (PatientId/DoctorId) must be present.");
        }

        if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate > filter.ToDate) {
            throw new BadRequestException("FromDate can't be after ToDate.");
        }

        var query = PrepareQueryWithFilters(filter);
        
        return (await query.ToListAsync())
            .Select(appointment => _mapper.Map<ViewAppointment>(appointment))
            .ToList();
    }

    private IQueryable<Appointment> PrepareQueryWithFilters(ViewAppointmentsFilter filter) {
        var query = _context.Appointments.AsQueryable();

        if (filter.FromDate.HasValue)
        {
            query = query.Where(a => a.AppointmentDateTime >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(a => a.AppointmentDateTime <= filter.ToDate.Value);
        }

        if (Guid.TryParse(filter.PatientId, out var patientId))
        {
            query = query.Where(a => a.PatientId.Equals(patientId));
        }

        if (Guid.TryParse(filter.DoctorId, out var doctorId))
        {
            query = query.Where(a => a.DoctorId.Equals(doctorId));
        }

        if (filter.Status is not null)
        {
            query = query.Where(a => a.Status.Equals(filter.Status));
        }

        return query;
    }
}
