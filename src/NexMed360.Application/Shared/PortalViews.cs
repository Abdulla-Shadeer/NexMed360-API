using NexMed360Api.Entities;
using NexMed360Api.Dtos.Appointment;
using NexMed360Api.Dtos.ClinicRecord;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.Doctor;
using NexMed360Api.Dtos.Receptionist;
using NexMed360Api.Dtos.HospitalAdmin;
using NexMed360Api.Dtos.Hospital;
using NexMed360Api.Dtos.User;
using NexMed360Api.Dtos.doctorSession;
using NexMed360Api.Entities.Enums;

namespace NexMed360.Application.Shared;

// All conversion to the existing response DTOs belongs to Application.
public sealed class PortalViews(IPortalReadStore store)
{
    private static AccountSummary? Account(PortalReadData data, string? id) => id == null ? null : data.Accounts.GetValueOrDefault(id);
    private static Hospital? HospitalRow(PortalReadData data, Guid id) => data.Hospitals.GetValueOrDefault(id);
    private static string? Address(Hospital? row) => row == null ? null : $"{row.Street}, {row.City}, {row.District}";

    public async Task<List<ViewAppointmentDto>> Appointments(IEnumerable<Appointment> source)
    {
        var rows = source.ToList(); var request = new PortalReadRequest();
        foreach (var row in rows) { request.PatientIds.Add(row.PatientId); request.DoctorIds.Add(row.DoctorId); request.HospitalIds.Add(row.HospitalId); request.SlotIds.Add(row.SessionSlotId); request.AccountIds.Add(row.CreatedByUserId); }
        var data = await store.Load(request);
        return rows.Select(row => AppointmentDto(row, data)).ToList();
    }
    public async Task<ViewAppointmentDto?> Appointment(Appointment? row) => row == null ? null : (await Appointments([row]))[0];
    private static ViewAppointmentDto AppointmentDto(Appointment row, PortalReadData data)
    {
        var patient = data.Patients.GetValueOrDefault(row.PatientId); var doctor = data.Doctors.GetValueOrDefault(row.DoctorId);
        var patientAccount = Account(data, patient?.UserId); var hospital = HospitalRow(data, row.HospitalId);
        return new ViewAppointmentDto
        {
            BookingReference = row.Id.ToString(),
            PatientId = row.PatientId.ToString(),
            PatientName = patientAccount?.FullName!,
            PatientEmail = patientAccount?.Email!,
            PatientPhone = patientAccount?.PhoneNumber!,
            DoctorId = row.DoctorId.ToString(),
            DoctorName = Account(data, doctor?.UserId)?.FullName!,
            DoctorSpeciality = doctor?.Speciality!,
            HospitalId = row.HospitalId.ToString(),
            HospitalName = hospital?.Name!,
            HospitalAddress = Address(hospital)!,
            HospitalPhone = hospital?.Phone!,
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
        var request = new PortalReadRequest(); request.AccountIds.Add(row.UserId);
        return PatientDto(row, await store.Load(request));
    }
    private static ViewPatientDto PatientDto(Patient row, PortalReadData data)
    {
        var user = Account(data, row.UserId);
        return new ViewPatientDto
        {
            Id = row.Id.ToString(),
            UserId = row.UserId,
            FirstName = user?.FirstName!,
            LastName = user?.LastName!,
            Email = user?.Email!,
            PhoneNumber = user?.PhoneNumber!,
            BirthDate = row.BirthDate,
            Gender = row.Gender
        };
    }
    public async Task<ViewDoctorDto?> Doctor(Doctor? row)
    {
        if (row == null) return null; var request = new PortalReadRequest(); request.HospitalIds.Add(row.HospitalId); request.AccountIds.Add(row.UserId);
        return DoctorDto(row, await store.Load(request));
    }
    private static ViewDoctorDto DoctorDto(Doctor row, PortalReadData data)
    {
        var user = Account(data, row.UserId);
        return new ViewDoctorDto
        {
            Id = row.Id.ToString(),
            UserId = row.UserId,
            FirstName = user?.FirstName!,
            LastName = user?.LastName!,
            Email = user?.Email!,
            PhoneNumber = user?.PhoneNumber!,
            Speciality = row.Speciality,
            HospitalId = row.HospitalId.ToString(),
            HospitalName = HospitalRow(data, row.HospitalId)?.Name!
        };
    }
    public async Task<List<ViewDoctorDto>> Doctors(IEnumerable<Doctor> source)
    {
        var rows = source.ToList(); var request = new PortalReadRequest(); foreach (var row in rows) { request.HospitalIds.Add(row.HospitalId); request.AccountIds.Add(row.UserId); }
        var data = await store.Load(request); return rows.Select(row => DoctorDto(row, data)).ToList();
    }
    public async Task<ViewReceptionistDto?> Receptionist(Receptionist? row)
    {
        if (row == null) return null; var request = new PortalReadRequest(); request.HospitalIds.Add(row.HospitalId); request.AccountIds.Add(row.UserId);
        return ReceptionistDto(row, await store.Load(request));
    }
    private static ViewReceptionistDto ReceptionistDto(Receptionist row, PortalReadData data)
    {
        var user = Account(data, row.UserId);
        return new ViewReceptionistDto
        {
            Id = row.Id.ToString(),
            UserId = row.UserId,
            FirstName = user?.FirstName!,
            LastName = user?.LastName!,
            Email = user?.Email!,
            PhoneNumber = user?.PhoneNumber!,
            Location = row.Location,
            Department = row.Department,
            HospitalId = row.HospitalId.ToString(),
            HospitalName = HospitalRow(data, row.HospitalId)?.Name!
        };
    }
    public async Task<ViewHospitalAdminDto?> Admin(HospitalAdmin? row)
    {
        if (row == null) return null; var request = new PortalReadRequest(); request.HospitalIds.Add(row.HospitalId); request.AccountIds.Add(row.UserId);
        return AdminDto(row, await store.Load(request));
    }
    private static ViewHospitalAdminDto AdminDto(HospitalAdmin row, PortalReadData data)
    {
        var user = Account(data, row.UserId);
        return new ViewHospitalAdminDto
        {
            Id = row.Id.ToString(),
            UserId = row.UserId,
            HospitalId = row.HospitalId.ToString(),
            HospitalName = HospitalRow(data, row.HospitalId)?.Name!,
            FirstName = user?.FirstName!,
            LastName = user?.LastName!,
            Email = user?.Email!,
            PhoneNumber = user?.PhoneNumber!
        };
    }
    public async Task<ViewHospitalDto?> Hospital(Hospital? row, bool includeAdmins = false)
    {
        if (row == null) return null;
        var request = new PortalReadRequest { IncludeHospitalAdmins = includeAdmins, IncludeAccountRoles = includeAdmins }; request.HospitalIds.Add(row.Id);
        return HospitalDto(row, await store.Load(request));
    }
    public Task<List<ViewHospitalDto>> Hospitals(IEnumerable<Hospital> rows)
        => Task.FromResult(rows.Select(row => HospitalDto(row, new PortalReadData())).ToList());
    private static ViewHospitalDto HospitalDto(Hospital row, PortalReadData data) => new()
    {
        Id = row.Id.ToString(),
        Name = row.Name,
        Street = row.Street,
        City = row.City,
        District = row.District,
        Phone = row.Phone,
        Grade = row.Grade,
        Admins = data.Admins.Where(admin => admin.HospitalId == row.Id).Select(admin =>
        {
            var dto = UserDto(admin.UserId, data);
            dto.HospitalAdminProfile = AdminDto(admin, data);
            return dto;
        }).ToList()
    };
    private static ViewUserDto UserDto(string userId, PortalReadData data)
    {
        var user = Account(data, userId);
        return new ViewUserDto
        {
            Id = userId,
            FirstName = user?.FirstName!,
            LastName = user?.LastName!,
            Email = user?.Email!,
            PhoneNumber = user?.PhoneNumber!,
            Roles = user?.Roles ?? []
        };
    }
    public async Task<List<ViewUserDto>> Members(IEnumerable<Doctor> doctors, IEnumerable<Receptionist> receptionists)
    {
        var doctorRows = doctors.ToList();
        var receptionistRows = receptionists.ToList();
        var request = new PortalReadRequest { IncludeAccountRoles = true };
        request.AccountIds.AddRange(doctorRows.Select(row => row.UserId).Concat(receptionistRows.Select(row => row.UserId)));
        request.HospitalIds.AddRange(doctorRows.Select(row => row.HospitalId).Concat(receptionistRows.Select(row => row.HospitalId)));
        var data = await store.Load(request);
        return doctorRows.Select(row =>
        {
            var dto = UserDto(row.UserId, data);
            dto.DoctorProfile = DoctorDto(row, data);
            return dto;
        }).Concat(receptionistRows.Select(row =>
        {
            var dto = UserDto(row.UserId, data);
            dto.ReceptionistProfile = ReceptionistDto(row, data);
            return dto;
        })).ToList();
    }
    public async Task<List<ViewClinicRecordDto>> Records(IEnumerable<ClinicRecord> source)
    {
        var rows = source.ToList(); var request = new PortalReadRequest();
        foreach (var row in rows) { request.PatientIds.Add(row.PatientId); request.DoctorIds.Add(row.DoctorId); }
        var data = await store.Load(request);
        return rows.Select(row =>
        {
            var doctor = data.Doctors.GetValueOrDefault(row.DoctorId); var patient = data.Patients.GetValueOrDefault(row.PatientId);
            return new ViewClinicRecordDto
            {
                Id = row.Id.ToString(),
                PatientName = Account(data, patient?.UserId)?.FullName!,
                DoctorName = Account(data, doctor?.UserId)?.FullName!,
                // ClinicRecord has no durable originating-hospital ID. Do not infer it from a current doctor assignment.
                HospitalName = null!,
                VisitDate = row.VisitDate,
                Diagnosis = row.Diagnosis,
                DoctorNotes = row.DoctorNotes,
                FollowUpDate = row.FollowUpDate,
                Prescriptions = row.Prescriptions?.Select(prescription => new ViewPrescriptionDto
                {
                    Id = prescription.Id.ToString(),
                    ClinicRecordId = prescription.ClinicRecordId.ToString(),
                    IssuedOn = row.VisitDate,
                    IssuedByDoctor = Account(data, doctor?.UserId)?.FullName!,
                    IssuedAtHospital = doctor == null ? null! : HospitalRow(data, doctor.HospitalId)?.Name!,
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
    public async Task<ViewClinicRecordDto?> Record(ClinicRecord? row) => row == null ? null : (await Records([row]))[0];
    public async Task<List<ViewDoctorSessionDto>> Sessions(IEnumerable<DoctorSession> source)
    {
        var rows = source.ToList(); var request = new PortalReadRequest(); request.HospitalIds.AddRange(rows.Select(row => row.HospitalId));
        var data = await store.Load(request);
        return rows.Select(row => new ViewDoctorSessionDto
        {
            Id = row.Id,
            DoctorId = row.DoctorId,
            HospitalId = row.HospitalId,
            HospitalName = HospitalRow(data, row.HospitalId)?.Name!,
            HospitalAddress = Address(HospitalRow(data, row.HospitalId))!,
            StartTime = row.StartTime,
            EndTime = row.EndTime,
            SlotDurationMinutes = row.SlotDurationMinutes,
            TotalSlots = row.TotalSlots,
            Occurrences = row.Occurrences,
            IsActive = row.IsActive
        }).ToList();
    }
    public async Task<ViewDoctorSessionDto?> Session(DoctorSession? row) => row == null ? null : (await Sessions([row]))[0];
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
        request.SessionIds.AddRange(rows.Select(row => row.DoctorSessionId)); var data = await store.Load(request);
        return rows.Select(row =>
        {
            var session = data.Sessions.GetValueOrDefault(row.DoctorSessionId); var doctor = session == null ? null : data.Doctors.GetValueOrDefault(session.DoctorId);
            return new ViewSessionHistoryDto
            {
                Id = row.Id.ToString(),
                DoctorSessionId = row.DoctorSessionId.ToString(),
                DoctorName = Account(data, doctor?.UserId)?.FullName!,
                DoctorSpeciality = doctor?.Speciality!,
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
    public static ViewCurrentOrNextSessionDto? Current(DoctorSession? row) => row == null ? null : new()
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
        SessionId = row.DoctorSessionId.ToString(),
        StartTime = row.DoctorSession.StartTime.ToString(),
        EndTime = row.DoctorSession.EndTime.ToString(),
        StartedTime = row.StartTime.ToString(),
        LastKnownAppointmentId = row.LastKnownAppointmentId?.ToString(),
        EncountersCount = row.EncountersCount
    };
    public async Task<ViewUserDto> User(AccountSummary account, bool includeProfiles = true)
    {
        var dto = new ViewUserDto { Id = account.Id, FirstName = account.FirstName!, LastName = account.LastName!, Email = account.Email!, PhoneNumber = account.PhoneNumber!, Roles = account.Roles };
        if (!includeProfiles) return dto;
        var request = new PortalReadRequest(); request.ProfileUserIds.Add(account.Id); var data = await store.Load(request);
        // Reuse the already loaded account fields when composing profiles.
        data.Accounts[account.Id] = account;
        if (account.Roles.Contains("Patient") && data.Patients.Values.FirstOrDefault(row => row.UserId == account.Id) is { } patient) dto.PatientProfile = PatientDto(patient, data);
        if (account.Roles.Contains("Doctor") && data.Doctors.Values.FirstOrDefault(row => row.UserId == account.Id) is { } doctor) dto.DoctorProfile = DoctorDto(doctor, data);
        if (account.Roles.Contains("Receptionist") && data.Receptionists.FirstOrDefault(row => row.UserId == account.Id) is { } receptionist) dto.ReceptionistProfile = ReceptionistDto(receptionist, data);
        if (account.Roles.Contains("HospitalAdmin") && data.Admins.FirstOrDefault(row => row.UserId == account.Id) is { } admin) dto.HospitalAdminProfile = AdminDto(admin, data);
        return dto;
    }
}
