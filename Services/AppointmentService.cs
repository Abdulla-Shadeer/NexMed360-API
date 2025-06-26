using AutoMapper;
using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
using FinalProjectApi.Data;
using FinalProjectApi.Dtos.Appointment;
using FinalProjectApi.Entities;
using FinalProjectApi.Entities.Enums;
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

    public async Task<Result<ViewAppointmentDto>> CreateAppointment(CreateAppointmentDto createAppointment)
    {
        if (!Guid.TryParse(createAppointment.DoctorId, out var doctorId))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Doctor ID format.");

        if (!Guid.TryParse(createAppointment.PatientId, out var patientId))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Patient ID format.");

        if (!Guid.TryParse(createAppointment.CreatedById, out var createdById))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid CreatedBy ID format.");

        if (!await _context.Users.AnyAsync(u => u.Id == createdById.ToString()))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.USR_NotFound, $"User with Id '{createdById}' not found!");

        var doctorCheck = IsPersonValidAndAvailable(doctorId, true, createAppointment.AppointmentDateTime, createAppointment.Duration, true);
        if (!doctorCheck.IsSuccess)
            return Result<ViewAppointmentDto>.Failure(doctorCheck.Error!);

        var patientCheck = IsPersonValidAndAvailable(patientId, false, createAppointment.AppointmentDateTime, createAppointment.Duration, true);
        if (!patientCheck.IsSuccess)
            return Result<ViewAppointmentDto>.Failure(patientCheck.Error!);

        var appointmentEntry = (await _context.Appointments.AddAsync(_mapper.Map<Appointment>(createAppointment)));
        await _context.SaveChangesAsync();
        return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(appointmentEntry.Entity));
    }

    public async Task<Result<List<ViewAppointmentDto>>> ViewAppointments(ViewAppointmentsFilterDto filter)
    {
        if (filter == null || (filter.PatientId.IsNullOrEmpty() && filter.DoctorId.IsNullOrEmpty()))
            return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.APPT_MinimumFilterRequirement);


        if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate > filter.ToDate)
            return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.APPT_FromDateAfterToDate);

        if (Guid.TryParse(filter.PatientId, out var patientId))
        {
            if (!IsValidPerson(patientId, false))
                return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.APPT_PatientNotFound);
        }

        if (Guid.TryParse(filter.DoctorId, out var doctorId))
            if (!IsValidPerson(doctorId, true))
                return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.APPT_DoctorNotFound);


        var query = PrepareQueryWithFilters(filter);

        var appointments = (await query.ToListAsync())
            .Select(appointment => _mapper.Map<ViewAppointmentDto>(appointment))
            .ToList();

        if (appointments.Count < 1)
            return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.APPT_NotFound, "Please adjust the filter and try again.");

        return Result<List<ViewAppointmentDto>>.Success(appointments);
    }

    private IQueryable<Appointment> PrepareQueryWithFilters(ViewAppointmentsFilterDto filter)
    {
        var query = _context.Appointments.AsQueryable();

        if (filter.FromDate.HasValue)
            query = query.Where(a => a.AppointmentDateTime.Date >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(a => a.AppointmentDateTime.Date <= filter.ToDate.Value);

        if (Guid.TryParse(filter.PatientId, out var patientId))
            query = query.Where(a => a.PatientId.Equals(patientId));

        if (Guid.TryParse(filter.DoctorId, out var doctorId))
            query = query.Where(a => a.DoctorId.Equals(doctorId));

        if (filter.Status is not null)
            query = query.Where(a => a.Status.Equals(filter.Status));

        return query;
    }

    public async Task<Result<ViewAppointmentDto>> ModifyAppointment(ModifyAppointmentDto modifyAppointmentDto)
    {
        if (!Guid.TryParse(modifyAppointmentDto.Id, out var appointmentId))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid 'appointmentId' format.");

        if (!Guid.TryParse(modifyAppointmentDto.RequesterId, out var requesterId))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid 'requesterId' format.");

        var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);

        if (appointment.CreatedById != requesterId)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_UnauthorizedEdit);

        if (appointment.Status == AppointmentStatus.Cancelled)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_Cancelled);

        var doctorId = Guid.TryParse(modifyAppointmentDto.DoctorId, out var newDoctorId) ? newDoctorId : appointment.DoctorId;
        var patientId = Guid.TryParse(modifyAppointmentDto.PatientId, out var newPatientId) ? newPatientId : appointment.PatientId;
        var apptDateAndTime = modifyAppointmentDto.AppointmentDateTime ?? appointment.AppointmentDateTime;
        var apptDuration = modifyAppointmentDto.Duration ?? appointment.Duration;
        var shouldCheckAvailability = modifyAppointmentDto.AppointmentDateTime.HasValue || modifyAppointmentDto.Duration.HasValue;

        var doctorCheck = IsPersonValidAndAvailable(doctorId, true, apptDateAndTime, apptDuration, shouldCheckAvailability);
        if (!doctorCheck.IsSuccess)
            return Result<ViewAppointmentDto>.Failure(doctorCheck.Error!);

        var patientCheck = IsPersonValidAndAvailable(patientId, false, apptDateAndTime, apptDuration, shouldCheckAvailability);
        if (!patientCheck.IsSuccess)
            return Result<ViewAppointmentDto>.Failure(patientCheck.Error!);

        appointment.DoctorId = doctorId;
        appointment.PatientId = patientId;
        appointment.AppointmentDateTime = apptDateAndTime;
        appointment.Duration = apptDuration;
        appointment.Location = modifyAppointmentDto.Location ?? appointment.Location;
        appointment.Remarks = modifyAppointmentDto.Remarks ?? appointment.Remarks;
        appointment.Status = appointment.Status; // Status is not modified in this method

        await _context.SaveChangesAsync();
        return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(appointment));
    }

    private Result<bool> IsPersonValidAndAvailable(Guid personId, bool isDoctor, DateTime appointmentDateTime, TimeSpan duration, bool shouldCheckAvailability)
    {
        if (!IsValidPerson(personId, isDoctor))
            return Result<bool>.Failure(isDoctor ? ErrorCode.APPT_DoctorNotFound : ErrorCode.APPT_PatientNotFound);

        if (shouldCheckAvailability && !AppointmentHelper.IsTimeSlotAvailable(_context, personId, appointmentDateTime, duration, isDoctor))
            return Result<bool>.Failure(isDoctor ? ErrorCode.APPT_DoctorUnavailable : ErrorCode.APPT_PatientUnavailable);

        return Result<bool>.Success(true);
    }

    private bool IsValidPerson(Guid id, bool isDoctor)
    {
        return isDoctor
            ? _context.Doctors.Any(d => d.Id == id)
            : _context.Patients.Any(p => p.Id == id);
    }

    public async Task<Result<ViewAppointmentDto>> ViewAppointment(string appointmentId)
    {
        if (!Guid.TryParse(appointmentId, out var id))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid);

        var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == id);

        if (appointment == null)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);

        return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(appointment));
    }

    public async Task<Result<ViewAppointmentDto>> ConfirmAppointment(string appointmentId)
    {
        if (!Guid.TryParse(appointmentId, out var id))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid);

        var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == id);
        if (appointment == null)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);

        if (appointment.Status == AppointmentStatus.Cancelled)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_Cancelled);

        // Reset cancellation request
        if (appointment.Status == AppointmentStatus.CancelRequested)
        {
            var cancellationRequest = await _context.CancellationRequests.FirstOrDefaultAsync(cr => cr.AppointmentId == id);
            _context.CancellationRequests.Remove(cancellationRequest!);
            await _context.SaveChangesAsync();
        }

        appointment.Status = AppointmentStatus.Confirmed;
        await _context.SaveChangesAsync();
        return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(appointment));
    }

    public async Task<Result<ViewAppointmentDto>> CancelAppointment(CancellationRequestDto cancellationRequestDto)
    {
        if (!Guid.TryParse(cancellationRequestDto.AppointmentId, out var appointmentId)
            || !Guid.TryParse(cancellationRequestDto.RequestedById, out var requesterId))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid);

        var appointment = await _context.Appointments.FirstOrDefaultAsync(ap => ap.Id == appointmentId);
        if (appointment == null)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);

        if (!await _context.Users.AnyAsync(u => u.Id == requesterId.ToString()))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.USR_NotFound);

        if (appointment.Status == AppointmentStatus.Cancelled)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_Cancelled);

        if (appointment.Status == AppointmentStatus.CancelRequested && appointment.CreatedById == requesterId)
        {
            appointment.Status = AppointmentStatus.Cancelled;
            var existingRequest = await _context.CancellationRequests.FirstOrDefaultAsync(cr => cr.AppointmentId == appointmentId);
            existingRequest!.Status = AppointmentStatus.Cancelled;

            await _context.SaveChangesAsync();
            return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(appointment));
        }

        if (appointment.Status == AppointmentStatus.CancelRequested && appointment.CreatedById != requesterId)
        {
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_CancellationAlreadyRequested);
        }

        // Now new cancellation request can be created
        var cancellationRequest = new CancellationRequest
        {
            AppointmentId = appointmentId,
            RequestedById = requesterId,
            Reason = cancellationRequestDto.Reason,
            CreatedDate = DateTime.UtcNow,
        };

        if (appointment.CreatedById == requesterId)
        {
            appointment.Status = AppointmentStatus.Cancelled;
            cancellationRequest.Status = AppointmentStatus.Cancelled;
        }
        else
        {
            appointment.Status = AppointmentStatus.CancelRequested;
            cancellationRequest.Status = AppointmentStatus.CancelRequested;
        }
        await _context.CancellationRequests.AddAsync(cancellationRequest);
        await _context.SaveChangesAsync();
        return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(appointment));
    }
}
