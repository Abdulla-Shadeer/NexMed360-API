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

    public async Task<Result<ViewAppointmentDto>> CreateAppointment(CreateAppointmentDto createAppointment, string requesterId)
    {
        if (!Guid.TryParse(createAppointment.DoctorId, out var doctorId))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Doctor ID format.");

        if (!Guid.TryParse(createAppointment.PatientId, out var patientId))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Patient ID format.");

        if (!Guid.TryParse(createAppointment.HospitalId, out var hospitalId))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid Hospital ID format.");

        if (!await _context.Hospitals.AnyAsync(h => h.Id == hospitalId))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.HOS_NotFound, $"Hospital with Id '{hospitalId}' not found!");

        if (!await _context.Hospitals.Where(h => h.Id == hospitalId).Include(h => h.Doctors).AnyAsync(h => h.Doctors.Any(d => d.Id == doctorId))) {
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_BadRequest, "Requested doctor is not available at the selected hospital!");
        }

        var doctorCheck = IsPersonValidAndAvailable(doctorId, true, createAppointment.AppointmentDateTime, true);
        if (!doctorCheck.IsSuccess)
            return Result<ViewAppointmentDto>.Failure(doctorCheck.Error!);

        var patientCheck = IsPersonValidAndAvailable(patientId, false, createAppointment.AppointmentDateTime, true);
        if (!patientCheck.IsSuccess)
            return Result<ViewAppointmentDto>.Failure(patientCheck.Error!);

        var newAppointment = _mapper.Map<Appointment>(createAppointment);
        newAppointment.CreatedByUserId = requesterId;

        var newAppointmentId = (await _context.Appointments.AddAsync(newAppointment)).Entity.Id;
        await _context.SaveChangesAsync();

        var createdAppointment = await LoadFullAppointmentData(newAppointmentId);
        return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(createdAppointment));
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
        else
        {
            return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Patient ID has invalid GUID format!");
        }

        if (Guid.TryParse(filter.DoctorId, out var doctorId))
        {
            if (!IsValidPerson(doctorId, true))
                return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.APPT_DoctorNotFound);
        }
        else
        {
            return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Doctor ID has invalid GUID format!");
        }

        if (Guid.TryParse(filter.HospitalId, out var hospitalId))
        {
            if (!await _context.Hospitals.AnyAsync(hs => hs.Id == hospitalId))
            {
                return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.HOS_NotFound, $"Hospital not found with ID {hospitalId}");
            }
        }
        else
        {
            return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Hospital ID has invalid GUID format!");
        }


        var query = PrepareQueryWithFilters(filter);

        var appointments = await query
            .Select(appt => _mapper.Map<ViewAppointmentDto>(appt))
            .ToListAsync();

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

        if (Guid.TryParse(filter.HospitalId, out var hospitalId))
            query = query.Where(a => a.HospitalId == hospitalId);

        if (filter.Status is not null)
            query = query.Where(a => a.Status.Equals(filter.Status));

        query = query.Include(appt => appt.Patient)
                .ThenInclude(pat => pat.User)
                .Include(appt => appt.Doctor)
                .ThenInclude(doc => doc.User)
                .Include(appt => appt.CreatedByUser)
                .Include(appt => appt.Hospital);

        return query;
    }

    public async Task<Result<ViewAppointmentDto>> ModifyAppointment(ModifyAppointmentDto modifyAppointmentDto, string requesterId)
    {
        if (!Guid.TryParse(modifyAppointmentDto.Id, out var appointmentId))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid 'appointmentId' format.");

        var appointment = await LoadFullAppointmentData(appointmentId);

        if (appointment == null)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);

        if (appointment.CreatedByUserId != requesterId)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_UnauthorizedEdit);

        if (appointment.Status == AppointmentStatus.Cancelled)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_Cancelled);

        var doctorId = Guid.TryParse(modifyAppointmentDto.DoctorId, out var newDoctorId) ? newDoctorId : appointment.DoctorId;
        var patientId = Guid.TryParse(modifyAppointmentDto.PatientId, out var newPatientId) ? newPatientId : appointment.PatientId;
        var apptDateAndTime = modifyAppointmentDto.AppointmentDateTime ?? appointment.AppointmentDateTime;
        var shouldCheckAvailability = modifyAppointmentDto.AppointmentDateTime.HasValue;

        var doctorCheck = IsPersonValidAndAvailable(doctorId, true, apptDateAndTime, shouldCheckAvailability);
        if (!doctorCheck.IsSuccess)
            return Result<ViewAppointmentDto>.Failure(doctorCheck.Error!);

        var patientCheck = IsPersonValidAndAvailable(patientId, false, apptDateAndTime, shouldCheckAvailability);
        if (!patientCheck.IsSuccess)
            return Result<ViewAppointmentDto>.Failure(patientCheck.Error!);

        appointment.DoctorId = doctorId;
        appointment.PatientId = patientId;
        appointment.AppointmentDateTime = apptDateAndTime;
        appointment.Remarks = modifyAppointmentDto.Remarks ?? appointment.Remarks;
        appointment.Status = appointment.Status; // Status is not modified in this method

        await _context.SaveChangesAsync();
        return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(appointment));
    }

    private Result<bool> IsPersonValidAndAvailable(Guid personId, bool isDoctor, DateTime appointmentDateTime, bool shouldCheckAvailability)
    {
        if (!IsValidPerson(personId, isDoctor))
            return Result<bool>.Failure(isDoctor ? ErrorCode.APPT_DoctorNotFound : ErrorCode.APPT_PatientNotFound);

        if (shouldCheckAvailability && !AppointmentHelper.IsTimeSlotAvailable(_context, personId, appointmentDateTime, isDoctor))
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

        var appointment = await LoadFullAppointmentData(id);

        if (appointment == null)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);

        return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(appointment));
    }

    public async Task<Result<ViewAppointmentDto>> ConfirmOrCompleteAppointment(string appointmentId, bool isComplete)
    {
        if (!Guid.TryParse(appointmentId, out var id))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid);

        var appointment = await LoadFullAppointmentData(id);
        if (appointment == null)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);

        if (appointment.Status == AppointmentStatus.Cancelled)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_Cancelled);

        // Reset cancellation request if exists
        if (appointment.Status == AppointmentStatus.CancelRequested)
        {
            var cancellationRequest = await _context.CancellationRequests.FirstOrDefaultAsync(cr => cr.AppointmentId == id);
            _context.CancellationRequests.Remove(cancellationRequest!);
            await _context.SaveChangesAsync();
        }

        appointment.Status = isComplete? AppointmentStatus.Completed: AppointmentStatus.Confirmed;
        await _context.SaveChangesAsync();
        return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(appointment));
    }

    public async Task<Result<ViewAppointmentDto>> CancelAppointment(CancellationRequestDto cancellationRequestDto, string requesterId)
    {
        if (!Guid.TryParse(cancellationRequestDto.AppointmentId, out var appointmentId))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid, "AppointmentId has invalid GUID format!");

        var appointment = await LoadFullAppointmentData(appointmentId);
        if (appointment == null)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);

        if (appointment.Status == AppointmentStatus.Cancelled)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_Cancelled);

        if (appointment.Status == AppointmentStatus.CancelRequested && appointment.CreatedByUserId == requesterId)
        {
            appointment.Status = AppointmentStatus.Cancelled;
            var existingRequest = await _context.CancellationRequests.FirstOrDefaultAsync(cr => cr.AppointmentId == appointmentId);
            existingRequest!.Status = AppointmentStatus.Cancelled;

            await _context.SaveChangesAsync();
            return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(appointment));
        }

        if (appointment.Status == AppointmentStatus.CancelRequested && appointment.CreatedByUserId != requesterId)
        {
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_CancellationAlreadyRequested);
        }

        // Now new cancellation request can be created
        var cancellationRequest = new CancellationRequest
        {
            AppointmentId = appointmentId,
            RequestedById = Guid.Parse(requesterId),
            Reason = cancellationRequestDto.Reason,
            CreatedDate = DateTime.UtcNow,
        };

        if (appointment.CreatedByUserId == requesterId)
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

    private async Task<Appointment?> LoadFullAppointmentData(Guid appointmentId)
    {
        return await _context.Appointments
                .Where(appt => appt.Id == appointmentId)
                .Include(appt => appt.Patient)
                .ThenInclude(pat => pat.User)
                .Include(appt => appt.Doctor)
                .ThenInclude(doc => doc.User)
                .Include(appt => appt.CreatedByUser)
                .Include(appt => appt.Hospital)
                .FirstOrDefaultAsync();
    }
}
