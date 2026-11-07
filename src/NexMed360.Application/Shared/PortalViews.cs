using NexMed360Api.Entities;
using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Dtos.ClinicalRecord;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Dtos.OrganizationMember;
using NexMed360Api.Dtos.OrganizationMember;
using NexMed360Api.Dtos.Organization;
using NexMed360Api.Dtos.User;
using NexMed360Api.Dtos.practitionerSchedule;
using NexMed360Api.Entities.Enums;

namespace NexMed360.Application.Shared;

// All conversion to the existing response DTOs belongs to Application.
public sealed class PortalViews(IPortalReadStore store)
{
    private static AccountSummary? Account(PortalReadData data, string? id) => id == null ? null : data.Accounts.GetValueOrDefault(id);
    private static AccountSummary? PersonAccount(PortalReadData data, Guid personId) => data.People.GetValueOrDefault(personId);
    private static AccountSummary? RoleAccount(PortalReadData data, PractitionerRole? role) => role == null ? null
        : data.Practitioners.TryGetValue(role.PractitionerId, out var practitioner) ? PersonAccount(data, practitioner.PersonId) : null;
    private static Organization? OrganizationRow(PortalReadData data, Guid id) => data.Organizations.GetValueOrDefault(id);
    private static string? Address(Organization? row) => row == null ? null : $"{row.Street}, {row.City}, {row.District}";

    public async Task<List<ViewAppointmentDto>> Appointments(IEnumerable<Appointment> source)
    {
        var rows = source.ToList(); var request = new PortalReadRequest();
        foreach (var row in rows) { request.PatientIds.Add(row.PatientId); request.PractitionerRoleIds.Add(row.PractitionerRoleId); request.OrganizationIds.Add(row.OrganizationId); request.SlotIds.Add(row.SessionSlotId); request.AccountIds.Add(row.CreatedByUserId); }
        var data = await store.Load(request);
        return rows.Select(row => AppointmentDto(row, data)).ToList();
    }
    public async Task<ViewAppointmentDto?> Appointment(Appointment? row) => row == null ? null : (await Appointments([row]))[0];
    private static ViewAppointmentDto AppointmentDto(Appointment row, PortalReadData data)
    {
        var patient = data.Patients.GetValueOrDefault(row.PatientId); var practitionerRole = data.PractitionerRoles.GetValueOrDefault(row.PractitionerRoleId);
        var patientAccount = patient == null ? null : PersonAccount(data, patient.PersonId); var organization = OrganizationRow(data, row.OrganizationId);
        return new ViewAppointmentDto
        {
            BookingReference = row.Id.ToString(),
            PatientId = row.PatientId.ToString(),
            PatientName = patientAccount?.FullName!,
            PatientEmail = patientAccount?.Email!,
            PatientPhone = patientAccount?.PhoneNumber!,
            PractitionerRoleId = row.PractitionerRoleId.ToString(),
            PractitionerName = RoleAccount(data, practitionerRole)?.FullName!,
            PractitionerSpecialty = practitionerRole?.Specialty!,
            OrganizationId = row.OrganizationId.ToString(),
            OrganizationName = organization?.Name!,
            OrganizationAddress = Address(organization)!,
            OrganizationPhone = organization?.Phone!,
            CreatedByUserId = row.CreatedByUserId,
            CreatedByUserName = Account(data, row.CreatedByUserId)?.FullName!,
            AppointmentDateTime = row.AppointmentDateTime.ToString(),
            AppointmentNumber = data.Slots.GetValueOrDefault(row.SessionSlotId)?.SlotNumber ?? 0,
            Remarks = row.Remarks,
            Status = row.Status
        };
    }
    public async Task<ViewPatientDto?> Patient(Patient? row)
    {
        if (row == null) return null;
        var request = new PortalReadRequest(); request.PersonIds.Add(row.PersonId);
        return PatientDto(row, await store.Load(request));
    }
    private static ViewPatientDto PatientDto(Patient row, PortalReadData data)
    {
        var user = PersonAccount(data, row.PersonId);
        return new ViewPatientDto
        {
            Id = row.Id.ToString(),
            UserId = user?.Id!,
            PersonId = row.PersonId.ToString(),
            FirstName = user?.FirstName!,
            LastName = user?.LastName!,
            Email = user?.Email!,
            PhoneNumber = user?.PhoneNumber!,
            BirthDate = row.BirthDate,
            Gender = row.Gender
        };
    }
    public async Task<ViewPractitionerRoleDto?> PractitionerRole(PractitionerRole? row)
    {
        if (row == null) return null; var request = new PortalReadRequest(); request.OrganizationIds.Add(row.OrganizationId); request.PractitionerRoleIds.Add(row.Id);
        return PractitionerRoleDto(row, await store.Load(request));
    }
    private static ViewPractitionerRoleDto PractitionerRoleDto(PractitionerRole row, PortalReadData data)
    {
        var user = RoleAccount(data, row);
        return new ViewPractitionerRoleDto
        {
            Id = row.Id.ToString(),
            UserId = user?.Id!,
            PractitionerId = row.PractitionerId.ToString(),
            RoleName = row.RoleName,
            FirstName = user?.FirstName!,
            LastName = user?.LastName!,
            Email = user?.Email!,
            PhoneNumber = user?.PhoneNumber!,
            Specialty = row.Specialty,
            OrganizationId = row.OrganizationId.ToString(),
            OrganizationName = OrganizationRow(data, row.OrganizationId)?.Name!
        };
    }
    public async Task<List<ViewPractitionerRoleDto>> PractitionerRoles(IEnumerable<PractitionerRole> source)
    {
        var rows = source.ToList(); var request = new PortalReadRequest(); foreach (var row in rows) { request.OrganizationIds.Add(row.OrganizationId); request.PractitionerRoleIds.Add(row.Id); }
        var data = await store.Load(request); return rows.Select(row => PractitionerRoleDto(row, data)).ToList();
    }
    public async Task<ViewOrganizationMemberDto?> OrganizationMembership(OrganizationMembership? row)
    {
        if (row == null) return null; var request = new PortalReadRequest(); request.OrganizationIds.Add(row.OrganizationId); request.AccountIds.Add(row.UserId);
        return OrganizationMemberDto(row, await store.Load(request));
    }
    private static ViewOrganizationMemberDto OrganizationMemberDto(OrganizationMembership row, PortalReadData data)
    {
        var user = Account(data, row.UserId);
        return new ViewOrganizationMemberDto
        {
            Id = row.Id.ToString(),
            UserId = row.UserId,
            FirstName = user?.FirstName!,
            LastName = user?.LastName!,
            Email = user?.Email!,
            PhoneNumber = user?.PhoneNumber!,
            Location = row.LocationId.HasValue ? data.Locations.GetValueOrDefault(row.LocationId.Value)?.Name : null,
            Department = null,
            OrganizationId = row.OrganizationId.ToString(),
            OrganizationName = OrganizationRow(data, row.OrganizationId)?.Name!
        };
    }
    public async Task<ViewOrganizationManagerDto?> Admin(OrganizationMembership? row)
    {
        if (row == null) return null; var request = new PortalReadRequest(); request.OrganizationIds.Add(row.OrganizationId); request.AccountIds.Add(row.UserId);
        return AdminDto(row, await store.Load(request));
    }
    private static ViewOrganizationManagerDto AdminDto(OrganizationMembership row, PortalReadData data)
    {
        var user = Account(data, row.UserId);
        return new ViewOrganizationManagerDto
        {
            Id = row.Id.ToString(),
            UserId = row.UserId,
            OrganizationId = row.OrganizationId.ToString(),
            OrganizationName = OrganizationRow(data, row.OrganizationId)?.Name!,
            FirstName = user?.FirstName!,
            LastName = user?.LastName!,
            Email = user?.Email!,
            PhoneNumber = user?.PhoneNumber!
        };
    }
    public async Task<ViewOrganizationDto?> Organization(Organization? row, bool includeAdmins = false)
    {
        if (row == null) return null;
        var request = new PortalReadRequest { IncludeOrganizationManagers = includeAdmins, IncludeAccountRoles = includeAdmins }; request.OrganizationIds.Add(row.Id);
        return OrganizationDto(row, await store.Load(request));
    }
    public Task<List<ViewOrganizationDto>> Organizations(IEnumerable<Organization> rows)
        => Task.FromResult(rows.Select(row => OrganizationDto(row, new PortalReadData())).ToList());
    private static ViewOrganizationDto OrganizationDto(Organization row, PortalReadData data) => new()
    {
        Id = row.Id.ToString(),
        Name = row.Name,
        Type = row.Type,
        Street = row.Street,
        City = row.City,
        District = row.District,
        Phone = row.Phone,
        Grade = row.Grade,
        Admins = data.Memberships.Where(admin => admin.OrganizationId == row.Id).Select(admin =>
        {
            var dto = UserDto(admin.UserId, data);
            dto.OrganizationManagerProfile = AdminDto(admin, data);
            return dto;
        }).ToList()
    };
    private static ViewUserDto UserDto(string userId, PortalReadData data)
    {
        var user = Account(data, userId);
        return new ViewUserDto
        {
            Id = userId,
            PersonId = user?.PersonId.ToString() ?? "",
            FirstName = user?.FirstName!,
            LastName = user?.LastName!,
            Email = user?.Email!,
            PhoneNumber = user?.PhoneNumber!,
            Roles = user?.Roles ?? []
        };
    }
    public async Task<List<ViewUserDto>> Members(IEnumerable<OrganizationMembership> memberships)
    {
        var rows = memberships.ToList();
        var request = new PortalReadRequest { IncludeAccountRoles = true };
        request.AccountIds.AddRange(rows.Select(row => row.UserId));
        request.ProfileUserIds.AddRange(rows.Select(row => row.UserId));
        request.OrganizationIds.AddRange(rows.Select(row => row.OrganizationId));
        var data = await store.Load(request);
        return rows.Select(row =>
        {
            var dto = UserDto(row.UserId, data);
            dto.OrganizationMemberProfile = OrganizationMemberDto(row, data);
            dto.Memberships = [dto.OrganizationMemberProfile];
            dto.PractitionerRoleProfiles = data.PractitionerRoles.Values.Where(role => role.OrganizationId == row.OrganizationId
                && RoleAccount(data, role)?.Id == row.UserId).Select(role => PractitionerRoleDto(role, data)).ToList();
            dto.PractitionerRoleProfile = dto.PractitionerRoleProfiles.FirstOrDefault();
            return dto;
        }).ToList();
    }
    public async Task<List<ViewClinicalRecordDto>> Records(IEnumerable<ClinicalRecord> source)
    {
        var rows = source.ToList(); var request = new PortalReadRequest();
        foreach (var row in rows) { request.PatientIds.Add(row.PatientId); request.PractitionerRoleIds.Add(row.PractitionerRoleId); request.OrganizationIds.Add(row.OrganizationId); }
        var data = await store.Load(request);
        return rows.Select(row =>
        {
            var practitionerRole = data.PractitionerRoles.GetValueOrDefault(row.PractitionerRoleId); var patient = data.Patients.GetValueOrDefault(row.PatientId);
            return new ViewClinicalRecordDto
            {
                Id = row.Id.ToString(),
                PatientName = (patient == null ? null : PersonAccount(data, patient.PersonId))?.FullName!,
                PractitionerName = RoleAccount(data, practitionerRole)?.FullName!,
                OrganizationName = OrganizationRow(data, row.OrganizationId)?.Name!,
                VisitDate = row.VisitDate,
                Diagnosis = row.Diagnosis,
                PractitionerNotes = row.PractitionerNotes,
                FollowUpDate = row.FollowUpDate,
                Prescriptions = row.Prescriptions?.Select(prescription => new ViewPrescriptionDto
                {
                    Id = prescription.Id.ToString(),
                    ClinicalRecordId = prescription.ClinicalRecordId.ToString(),
                    IssuedOn = row.VisitDate,
                    IssuedByPractitioner = RoleAccount(data, practitionerRole)?.FullName!,
                    IssuedAtOrganization = OrganizationRow(data, row.OrganizationId)?.Name!,
                    MedicineName = prescription.MedicineName,
                    Dosage = prescription.Dosage,
                    TimesPerDay = prescription.TimesPerDay,
                    IsLongTermUse = prescription.IsLongTermUse,
                    Duration = prescription.Duration,
                    Notes = prescription.Notes,
                    IsActive = prescription.IsActive
                }).ToList() ?? []
            };
        }).ToList();
    }
    public async Task<ViewClinicalRecordDto?> Record(ClinicalRecord? row) => row == null ? null : (await Records([row]))[0];
    public async Task<List<ViewPractitionerScheduleDto>> Sessions(IEnumerable<PractitionerSchedule> source)
    {
        var rows = source.ToList(); var request = new PortalReadRequest(); request.OrganizationIds.AddRange(rows.Select(row => row.OrganizationId));
        var data = await store.Load(request);
        return rows.Select(row => new ViewPractitionerScheduleDto
        {
            Id = row.Id,
            PractitionerRoleId = row.PractitionerRoleId,
            OrganizationId = row.OrganizationId,
            OrganizationName = OrganizationRow(data, row.OrganizationId)?.Name!,
            OrganizationAddress = Address(OrganizationRow(data, row.OrganizationId))!,
            StartTime = row.StartTime,
            EndTime = row.EndTime,
            SlotDurationMinutes = row.SlotDurationMinutes,
            TotalSlots = row.TotalSlots,
            Occurrences = row.Occurrences,
            IsActive = row.IsActive
        }).ToList();
    }
    public async Task<ViewPractitionerScheduleDto?> Session(PractitionerSchedule? row) => row == null ? null : (await Sessions([row]))[0];
    public static ViewSessionSlotDto Slot(SessionSlot row) => new()
    {
        Id = row.Id,
        SessionId = row.SessionId,
        StartTime = row.StartTime,
        EndTime = row.EndTime,
        Date = row.Date,
        IsAvailable = row.IsAvailable,
        BookingAllowed = row.BookingAllowed,
        SlotNumber = row.SlotNumber
    };
    public async Task<List<ViewSessionHistoryDto>> Histories(IEnumerable<SessionHistory> source)
    {
        var rows = source.ToList(); var request = new PortalReadRequest();
        request.SessionIds.AddRange(rows.Select(row => row.PractitionerScheduleId)); var data = await store.Load(request);
        return rows.Select(row =>
        {
            var session = data.Sessions.GetValueOrDefault(row.PractitionerScheduleId); var practitionerRole = session == null ? null : data.PractitionerRoles.GetValueOrDefault(session.PractitionerRoleId);
            return new ViewSessionHistoryDto
            {
                Id = row.Id.ToString(),
                PractitionerScheduleId = row.PractitionerScheduleId.ToString(),
                PractitionerName = RoleAccount(data, practitionerRole)?.FullName!,
                PractitionerSpecialty = practitionerRole?.Specialty!,
                Date = row.Date,
                StartTime = session?.StartTime ?? default,
                EndTime = session?.EndTime ?? default,
                StartedTime = row.StartTime,
                EndedTime = row.EndTime ?? default,
                SessionStatus = row.SessionStatus,
                EncountersCount = row.EncountersCount,
                LastKnownAppointmentId = row.LastKnownAppointmentId?.ToString()
            };
        }).ToList();
    }
    public async Task<ViewSessionHistoryDto?> History(SessionHistory? row) => row == null ? null : (await Histories([row]))[0];
    public static ViewCurrentOrNextSessionDto? Current(PractitionerSchedule? row) => row == null ? null : new()
    {
        IsCurrent = row.History.Any(),
        SessionId = row.Id.ToString(),
        StartTime = row.StartTime.ToString(),
        EndTime = row.EndTime.ToString(),
        StartedTime = row.History.FirstOrDefault()?.StartTime.ToString(),
        LastKnownAppointmentId = row.History.FirstOrDefault()?.LastKnownAppointmentId?.ToString(),
        EncountersCount = row.History.FirstOrDefault()?.EncountersCount ?? 0
    };
    public static ViewCurrentOrNextSessionDto Current(SessionHistory row) => new()
    {
        IsCurrent = row.SessionStatus == SessionStatus.Started,
        SessionId = row.PractitionerScheduleId.ToString(),
        StartTime = row.PractitionerSchedule.StartTime.ToString(),
        EndTime = row.PractitionerSchedule.EndTime.ToString(),
        StartedTime = row.StartTime.ToString(),
        LastKnownAppointmentId = row.LastKnownAppointmentId?.ToString(),
        EncountersCount = row.EncountersCount
    };
    public async Task<ViewUserDto> User(AccountSummary account, bool includeProfiles = true)
    {
        var dto = new ViewUserDto { Id = account.Id, PersonId = account.PersonId.ToString(), FirstName = account.FirstName!, LastName = account.LastName!, Email = account.Email!, PhoneNumber = account.PhoneNumber!, Roles = account.Roles };
        if (!includeProfiles) return dto;
        var request = new PortalReadRequest(); request.ProfileUserIds.Add(account.Id); var data = await store.Load(request);
        // Reuse the already loaded account fields when composing profiles.
        data.Accounts[account.Id] = account;
        dto.PersonId = account.PersonId.ToString();
        if (data.Patients.Values.FirstOrDefault(row => row.PersonId == account.PersonId) is { } patient)
            dto.PatientProfile = PatientDto(patient, data);
        dto.PractitionerRoleProfiles = data.PractitionerRoles.Values.Where(row => RoleAccount(data, row)?.PersonId == account.PersonId)
            .Select(row => PractitionerRoleDto(row, data)).ToList();
        dto.Memberships = data.Memberships.Where(row => row.UserId == account.Id).Select(row => OrganizationMemberDto(row, data)).ToList();
        // Phase 2 replaces these current-screen defaults with explicit workspace selection.
        dto.PractitionerRoleProfile = dto.PractitionerRoleProfiles.FirstOrDefault();
        if (data.Memberships.FirstOrDefault(row => row.UserId == account.Id) is { } membership)
        {
            dto.OrganizationMemberProfile = OrganizationMemberDto(membership, data);
            if (account.Roles.Contains("OrganizationManager")) dto.OrganizationManagerProfile = AdminDto(membership, data);
        }
        return dto;
    }
}
