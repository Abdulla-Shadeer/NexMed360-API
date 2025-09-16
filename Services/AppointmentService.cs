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

    public async Task<Result<ViewAppointmentDto>> Create(CreateAppointmentDto createAppointment, string requesterId)
    {
        var slotToBook = await _context.SessionSlots
            .FirstOrDefaultAsync(ss => ss.Id == createAppointment.SessionSlotId);

        if (slotToBook == null) {
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_BadRequest, "No slots found with given ID!");
        }

        var session = await _context.DoctorSessions
            .FirstOrDefaultAsync(ds => ds.Id == slotToBook.SessionId);

        if (session == null) {
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_BadRequest, "No session found for given slot ID!");
        }

        var appointment = new Appointment() 
        { 
            PatientId = createAppointment.PatientId,
            DoctorId = session.DoctorId,
            HospitalId = session.HospitalId,
            CreatedByUserId = requesterId,
            AppointmentDateTime = slotToBook.Date.ToDateTime(slotToBook.StartTime),
            Number = slotToBook.SlotNumber,
            SessionSlotId = slotToBook.Id,
            Remarks = createAppointment.Remarks,
        };

        var newAppointmentId = (await _context.Appointments.AddAsync(appointment)).Entity.Id;
        slotToBook.IsAvailable = false;
        await _context.SaveChangesAsync();

        var createdAppointment = await LoadFullAppointmentData(newAppointmentId);
        return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(createdAppointment));
    }

    public async Task<Result<List<ViewAppointmentDto>>> GetOwn(ViewAppointmentsFilterDto filter, string userId)
    {
        var filterValidation = await ValidateFilter(filter);
        if (!filterValidation.IsSuccess) {
            return filterValidation;
        }

        var ownPatientId = await _context.Patients
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();
        var ownDoctorId = await _context.Doctors
            .Where(d => d.UserId == userId)
            .Select(d => d.Id)
            .FirstOrDefaultAsync();

        var baseQuery = _context.Appointments
            .Where(appt => appt.PatientId == ownPatientId || appt.DoctorId == ownDoctorId)
            .AsQueryable();

        return Result<List<ViewAppointmentDto>>.Success(await ApplyFilter(filter, baseQuery));
    }

    public async Task<Result<List<ViewAppointmentDto>>> GetHospitalAppointments(ViewAppointmentsFilterDto filter, string userId)
    {
        var filterValidation = await ValidateFilter(filter);
        if (!filterValidation.IsSuccess)
        {
            return filterValidation;
        }

        var ownHospitalId = await _context.Receptionists
            .Where(r => r.UserId == userId)
            .Select(r => r.HospitalId)
            .FirstOrDefaultAsync();

        var baseQuery = _context.Appointments
            .Where(appt => appt.HospitalId == ownHospitalId)
            .AsQueryable();

        return Result<List<ViewAppointmentDto>>.Success(await ApplyFilter(filter, baseQuery));
    }

    private async Task<Result<List<ViewAppointmentDto>>> ValidateFilter(ViewAppointmentsFilterDto filter) {
        if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate > filter.ToDate)
        {
            return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.APPT_FromDateAfterToDate);
        }

        if (filter.PatientId != null)
        {
            if (Guid.TryParse(filter.PatientId, out var patientId))
            {
                if (!IsValidPerson(patientId, false))
                    return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.APPT_PatientNotFound);
            }
            else
            {
                return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Patient ID has invalid GUID format!");
            }
        }

        if (filter.DoctorId != null)
        {
            if (Guid.TryParse(filter.DoctorId, out var doctorId))
            {
                if (!IsValidPerson(doctorId, true))
                    return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.APPT_DoctorNotFound);
            }
            else
            {
                return Result<List<ViewAppointmentDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Doctor ID has invalid GUID format!");
            }
        }

        if (filter.HospitalId != null)
        {
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
        }

        return Result<List<ViewAppointmentDto>>.Success([]);
    }

    private async Task<List<ViewAppointmentDto>> ApplyFilter(ViewAppointmentsFilterDto filter, IQueryable<Appointment> baseQuery)
    {
        if (filter.FromDate.HasValue)
            baseQuery = baseQuery.Where(a => a.AppointmentDateTime.Date >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            baseQuery = baseQuery.Where(a => a.AppointmentDateTime.Date <= filter.ToDate.Value);

        if (Guid.TryParse(filter.PatientId, out var patientId))
            baseQuery = baseQuery.Where(a => a.PatientId.Equals(patientId));

        if (Guid.TryParse(filter.DoctorId, out var doctorId))
            baseQuery = baseQuery.Where(a => a.DoctorId.Equals(doctorId));

        if (Guid.TryParse(filter.HospitalId, out var hospitalId))
            baseQuery = baseQuery.Where(a => a.HospitalId == hospitalId);

        if (filter.Status is not null)
            baseQuery = baseQuery.Where(a => a.Status == filter.Status);

        baseQuery = baseQuery.Include(appt => appt.Patient)
                .ThenInclude(pat => pat.User)
                .Include(appt => appt.Doctor)
                .ThenInclude(doc => doc.User)
                .Include(appt => appt.CreatedByUser)
                .Include(appt => appt.Hospital)
                .Include(appt => appt.SessionSlot);

        return await baseQuery
            .Select(appt => _mapper.Map<ViewAppointmentDto>(appt))
            .ToListAsync(); ;
    }

    public async Task<Result<ViewAppointmentDto>> Modify(ModifyAppointmentDto modifyAppointmentDto, string requesterId)
    {

        var appointment = await _context.Appointments.FirstOrDefaultAsync(appt => appt.Id == modifyAppointmentDto.Id);

        if (appointment == null)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);

        if (appointment.CreatedByUserId != requesterId)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_UnauthorizedEdit);

        if (appointment.Status == AppointmentStatus.Cancelled)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_Cancelled);

        appointment.Remarks = modifyAppointmentDto.Remarks ?? appointment.Remarks;
        appointment.Status = appointment.Status; // Status is not modified in this method

        if (modifyAppointmentDto.SlotId != null)
        {
            var newSlot = await _context.SessionSlots.FirstOrDefaultAsync(ss => ss.Id == modifyAppointmentDto.SlotId);
            if (newSlot == null)
                return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_BadRequest, "No session slots found with given ID");

            if (!newSlot.IsAvailable || !newSlot.BookingAllowed)
                return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_BadRequest, "Requested slot is unavailable.");

            var session = await _context.DoctorSessions.FirstOrDefaultAsync(ds => ds.Id == newSlot.SessionId);
            if (session == null)
                return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_BadRequest, "No associated session data found for the given slot ID");

            var oldSlotId = appointment.SessionSlotId; // to release the current slot
            appointment.SessionSlotId = newSlot.Id;
            appointment.DoctorId = session.DoctorId;
            appointment.HospitalId = session.HospitalId;
            appointment.Number = newSlot.SlotNumber;
            appointment.AppointmentDateTime = newSlot.Date.ToDateTime(newSlot.StartTime);

            // release old slot
            newSlot.IsAvailable = false;
            var oldSlot = await _context.SessionSlots.FirstOrDefaultAsync(ss => ss.Id == oldSlotId);
            oldSlot!.IsAvailable = true;
        }

        // patient update should happen after any date time update to prevent patient unavailable error
        var patientId = modifyAppointmentDto.PatientId ?? appointment.PatientId;
        var patientCheck = IsPersonValidAndAvailable(patientId, false, appointment.AppointmentDateTime, modifyAppointmentDto.SlotId != null);
        if (!patientCheck.IsSuccess)
            return Result<ViewAppointmentDto>.Failure(patientCheck.Error!);

        appointment.PatientId = patientId;

        await _context.SaveChangesAsync();
        return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(
            await LoadFullAppointmentData(appointment.Id)
            ));
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

    public async Task<Result<ViewAppointmentDto>> Get(string appointmentId)
    {
        if (!Guid.TryParse(appointmentId, out var id))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid);

        var appointment = await LoadFullAppointmentData(id);

        if (appointment == null)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);

        return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(appointment));
    }

    public async Task<Result<ViewAppointmentDto>> Cancel(CancellationRequestDto cancellationRequestDto, string requesterId)
    {
        if (!Guid.TryParse(cancellationRequestDto.AppointmentId, out var appointmentId))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid, "AppointmentId has invalid GUID format!");

        var appointment = await LoadFullAppointmentData(appointmentId);

        if (appointment == null)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);

        if (appointment.Status == AppointmentStatus.Cancelled)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_Cancelled);

        var bookedSlot = await _context.SessionSlots.FirstOrDefaultAsync(ss => ss.Id == appointment.SessionSlotId);

        if (appointment.Status == AppointmentStatus.CancelRequested && appointment.CreatedByUserId == requesterId)
        {
            appointment.Status = AppointmentStatus.Cancelled;
            var existingRequest = await _context.CancellationRequests.FirstOrDefaultAsync(cr => cr.AppointmentId == appointmentId);
            existingRequest!.Status = AppointmentStatus.Cancelled;

            bookedSlot!.IsAvailable = true;

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
            bookedSlot!.IsAvailable = true;
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
                .Include(appt => appt.SessionSlot)
                .FirstOrDefaultAsync();
    }

    public async Task<Result<ViewDoctorSessionDto>> CreateDoctorSession(CreateDoctorSessionDto dto)
    {
        if (!await _context.Doctors.AnyAsync(dr => dr.Id == dto.DoctorId))
        {
            return Result<ViewDoctorSessionDto>.Failure(ErrorCode.APPT_DoctorNotFound);
        }

        if (!await _context.Hospitals.AnyAsync(hs => hs.Id == dto.HospitalId))
        {
            return Result<ViewDoctorSessionDto>.Failure(ErrorCode.HOS_NotFound);
        }

        if (dto.StartTime > dto.EndTime)
        {
            return Result<ViewDoctorSessionDto>.Failure(ErrorCode.SYS_BadRequest, "Start time cannot be after End time!");
        }

        var doctorSessions = _context.DoctorSessions.AsEnumerable();

        // Todo: Also consider time overlapping
        // (ex, session A 6.00-10.00 and session B 8.00-11.00 will cause conflict appointment bookings for same doctor at same hospital and same date time)
        if (doctorSessions.Any(
            ds => ds.DoctorId == dto.DoctorId
            && ds.HospitalId == dto.HospitalId
            && ds.StartTime == dto.StartTime
            && ds.EndTime == dto.EndTime
            && ds.Occurrences.OrderBy(x => x).SequenceEqual(dto.Occurrences.OrderBy(x => x))
            )) {
            return Result<ViewDoctorSessionDto>.Failure(ErrorCode.SYS_DuplicateEntry, "Session already exist!");
        }

        var addedEntityId = (await _context.DoctorSessions.AddAsync(_mapper.Map<DoctorSession>(dto))).Entity.Id;
        await _context.SaveChangesAsync();

        var doctorSession = await _context.DoctorSessions
            .Where(ds => ds.Id == addedEntityId)
            .Include(ds => ds.Hospital)
            .Select(ds => _mapper.Map<ViewDoctorSessionDto>(ds))
            .FirstOrDefaultAsync();

        return Result<ViewDoctorSessionDto>.Success(doctorSession);
    }

    public async Task<Result<List<ViewDoctorSessionDto>>> GetDoctorSessions(Guid doctorId)
    {
        return Result<List<ViewDoctorSessionDto>>.Success(
            await _context.DoctorSessions
                .Where(ds => ds.DoctorId == doctorId)
                .Include(ds => ds.Hospital)
                .Select(ds => _mapper.Map<ViewDoctorSessionDto>(ds))
                .ToListAsync());
    }

    public async Task<Result<List<ViewSessionSlotDto>>> GetSessionSlots(GetSessionSlotsDto dto)
    {
        var session = await _context.DoctorSessions.FirstOrDefaultAsync(ds => ds.Id == dto.SessionId);

        if (session == null)
        {
            return Result<List<ViewSessionSlotDto>>.Failure(ErrorCode.SYS_BadRequest, "No sessions found with given ID");
        }

        // return if slots present with given date and id
        var existingSlots = await _context.SessionSlots
            .Where(ss => ss.SessionId == dto.SessionId && ss.Date == dto.Date)
            .Select(ss => _mapper.Map<ViewSessionSlotDto>(ss))
            .ToListAsync();

        if (existingSlots != null && !existingSlots.IsNullOrEmpty())
        {
            return Result<List<ViewSessionSlotDto>>.Success(existingSlots);
        }

        var slotGeneration = AppointmentHelper.CheckAndGenerateSlots(session, dto.Date);

        if (!slotGeneration.IsSuccess)
        {
            return Result<List<ViewSessionSlotDto>>.Failure(slotGeneration.Error!);
        }

        await _context.SessionSlots.AddRangeAsync(slotGeneration.Data!);
        await _context.SaveChangesAsync();

        return Result<List<ViewSessionSlotDto>>.Success(
            await _context.SessionSlots
            .Where(ss => ss.SessionId == dto.SessionId && ss.Date == dto.Date)
            .Select(ss => _mapper.Map<ViewSessionSlotDto>(ss))
            .ToListAsync()
            );
    }

    public async Task<Result<ViewAppointmentDto>> ChangeStatus(string appointmentId, AppointmentStatus status)
    {
        if (!Guid.TryParse(appointmentId, out var id))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid);

        var appointment = await LoadFullAppointmentData(id);
        if (appointment == null)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);

        if (appointment.Status == AppointmentStatus.Cancelled)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_Cancelled);

        // Reset cancellation request if exists
        if (appointment.Status == AppointmentStatus.CancelRequested && status != AppointmentStatus.CancelRequested)
        {
            var cancellationRequest = await _context.CancellationRequests.FirstOrDefaultAsync(cr => cr.AppointmentId == id);
            _context.CancellationRequests.Remove(cancellationRequest!);
            await _context.SaveChangesAsync();
        }

        appointment.Status = status;
        await _context.SaveChangesAsync();
        return Result<ViewAppointmentDto>.Success(_mapper.Map<ViewAppointmentDto>(appointment));
    }
}
