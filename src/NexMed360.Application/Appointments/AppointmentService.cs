using NexMed360.Application.Shared;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Entities;
using NexMed360Api.Entities.Enums;
using NexMed360Api.Interfaces.Services;
using NexMed360.Domain.Rules;

namespace NexMed360Api.Services;

public class AppointmentService(IAppointmentRepository repository, PortalViews views) : IAppointmentService
{
    public async Task<Result<ViewAppointmentDto>> Create(CreateAppointmentDto dto, string requesterId)
    {
        var slot = await repository.FindSlot(dto.SessionSlotId);
        if (slot == null) return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_BadRequest, "No slots found with given ID!");
        if (slot.Session == null) return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_BadRequest, "No session found for given slot ID!");
        var eligibility = AppointmentRules.Booking(slot.Session.IsActive, slot.IsAvailable, slot.BookingAllowed);
        if (eligibility == BookingEligibility.InactiveSession)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_PractitionerRoleUnavailable, "The practitioner session is inactive and cannot accept bookings.");
        if (eligibility == BookingEligibility.UnavailableSlot)
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_PractitionerRoleUnavailable, "The selected slot is not available for booking.");
        var appointment = Appointment.Book(dto.PatientId, slot, requesterId, dto.Remarks);
        if (!await repository.TryBook(appointment, slot))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_PractitionerRoleUnavailable, "The selected slot has already been booked. Please choose another slot.");
        return Result<ViewAppointmentDto>.Success(await views.Appointment(await repository.Find(appointment.Id)));
    }
    public async Task<Result<List<ViewAppointmentDto>>> GetOwn(ViewAppointmentsFilterDto filter, string userId)
    {
        var validation = await ValidateFilter(filter); 
        if (!validation.IsSuccess) return Result<List<ViewAppointmentDto>>.Failure(validation.Error!);
        return Result<List<ViewAppointmentDto>>.Success(await views.Appointments(await repository.GetOwn(validation.Data!, userId)));
    }
    public async Task<Result<List<ViewAppointmentDto>>> GetOrganizationAppointments(ViewAppointmentsFilterDto filter, string userId)
    {
        var validation = await ValidateFilter(filter); if (!validation.IsSuccess) return Result<List<ViewAppointmentDto>>.Failure(validation.Error!);
        return Result<List<ViewAppointmentDto>>.Success(await views.Appointments(await repository.GetOrganizationAppointments(validation.Data!, userId)));
    }
    private async Task<Result<AppointmentFilter>> ValidateFilter(ViewAppointmentsFilterDto filter)
    {
        if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate > filter.ToDate)
            return Result<AppointmentFilter>.Failure(ErrorCode.APPT_FromDateAfterToDate);
        Guid? patient = null, practitionerRole = null, organization = null;
        if (filter.PatientId != null)
        {
            if (!Guid.TryParse(filter.PatientId, out var id)) return Result<AppointmentFilter>.Failure(ErrorCode.SYS_InvalidGuid, "Patient ID has invalid GUID format!");
            if (!await repository.PatientExists(id)) return Result<AppointmentFilter>.Failure(ErrorCode.APPT_PatientNotFound); patient = id;
        }
        if (filter.PractitionerRoleId != null)
        {
            if (!Guid.TryParse(filter.PractitionerRoleId, out var id)) return Result<AppointmentFilter>.Failure(ErrorCode.SYS_InvalidGuid, "Practitioner ID has invalid GUID format!");
            if (!await repository.PractitionerRoleExists(id)) return Result<AppointmentFilter>.Failure(ErrorCode.APPT_PractitionerRoleNotFound); practitionerRole = id;
        }
        if (filter.OrganizationId != null)
        {
            if (!Guid.TryParse(filter.OrganizationId, out var id)) return Result<AppointmentFilter>.Failure(ErrorCode.SYS_InvalidGuid, "Organization ID has invalid GUID format!");
            if (!await repository.OrganizationExists(id)) return Result<AppointmentFilter>.Failure(ErrorCode.HOS_NotFound, $"Organization not found with ID {id}"); organization = id;
        }
        return Result<AppointmentFilter>.Success(new(filter.FromDate, filter.ToDate, patient, practitionerRole, organization, filter.Status));
    }
    public async Task<Result<ViewAppointmentDto>> Get(string id, string? caller, IReadOnlyCollection<string> roles)
    {
        if (string.IsNullOrWhiteSpace(caller)) return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_Unauthorized);
        if (!Guid.TryParse(id, out var appointmentId)) return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid);
        var appointment = await repository.FindReadable(appointmentId, caller, roles);
        if (appointment == null) return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);
        return Result<ViewAppointmentDto>.Success(await views.Appointment(appointment));
    }
    public async Task<Result<ViewAppointmentDto>> Cancel(CancellationRequestDto dto, string? caller, IReadOnlyCollection<string> roles)
    {
        if (string.IsNullOrWhiteSpace(caller)) return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_Unauthorized);
        if (!Guid.TryParse(dto.AppointmentId, out var id)) return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid, "AppointmentId has invalid GUID format!");
        var appointment = await repository.FindReadable(id, caller, roles);
        if (appointment == null) return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);
        if (appointment.IsCancelled) return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_Cancelled);
        if (appointment.HasCancellationRequest && !appointment.IsCreator(caller))
            return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_CancellationAlreadyRequested);
        var request = appointment.HasCancellationRequest ? await repository.FindCancellationRequest(id) : null;
        if (request == null)
        {
            request = CancellationRequest.Create(id, caller, dto.Reason);
            request.OrganizationId = appointment.OrganizationId;
            await repository.AddCancellationRequest(request);
        }
        var slot = await repository.FindSlot(appointment.SessionSlotId);
        appointment.Cancel(request, caller, slot!);
        await repository.SaveChanges();
        return Result<ViewAppointmentDto>.Success(await views.Appointment(appointment));
    }
    public async Task<Result<ViewAppointmentDto>> ChangeStatus(string id, AppointmentStatus status, string? caller, IReadOnlyCollection<string> roles)
    {
        if (string.IsNullOrWhiteSpace(caller)) return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_Unauthorized);
        if (!Guid.TryParse(id, out var appointmentId)) return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_InvalidGuid);
        var staffRoles = AppointmentRules.StatusRoles(roles, status);
        if (staffRoles.Length == 0) return Result<ViewAppointmentDto>.Failure(ErrorCode.SYS_Unauthorized, "Your role cannot perform this appointment status change.", 403);
        var appointment = await repository.FindReadable(appointmentId, caller, staffRoles);
        if (appointment == null) return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_NotFound);
        if (appointment.IsCancelled) return Result<ViewAppointmentDto>.Failure(ErrorCode.APPT_Cancelled);
        if (appointment.HasCancellationRequest && status != AppointmentStatus.CancelRequested)
        {
            var request = await repository.FindCancellationRequest(appointmentId);
            if (request != null) repository.RemoveCancellationRequest(request);
        }
        appointment.ChangeStatus(status);
        await repository.SaveChanges();
        return Result<ViewAppointmentDto>.Success(await views.Appointment(appointment));
    }
}
