using FinalProjectApi.Data;
using FinalProjectApi.Dtos.ClinicRecord;
using FinalProjectApi.Dtos.Patient;
using FinalProjectApi.Entities;
using FinalProjectApi.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectApi.Services;

public class PatientService : IPatientService
{
    private readonly IUserService _userService;
    private readonly ApplicationDbContext _dbContext;

    public PatientService(IUserService userService, ApplicationDbContext dbContext)
    {
        _userService = userService;
        _dbContext = dbContext;
    }

    public async Task<bool> AddToVisitHistory(AddToVisitHistoryDto dto)
    {
        var visitHistory = new VisitHistory();
        visitHistory.HospitalId = dto.HospitalId;
        visitHistory.Reason = dto.Reason;
        await _dbContext.VisitHistorys.AddAsync(visitHistory);
        return (await _dbContext.SaveChangesAsync()) == 1;
    }

    public async Task<string> CreatePatient(CreatePatientDto dto)
    {
        var userId = await _userService.RegisterUser(dto.PersonalInformation);

        if (userId != null)
        {
            var patient = new Patient();
            patient.UserId = userId;
            patient.Gender = dto.Gender;
            patient.BirthDate = dto.BirthDate;

            var createdPatient = await _dbContext.Patients.AddAsync(patient);
            await _dbContext.SaveChangesAsync();
            return createdPatient.Entity.Id.ToString();
        }
        return null;
    }

    public async Task<ViewPatientDto> ViewPatient(string id)
    {
        // todo map user and patient details to ViewPatientDto
        var patient = await _dbContext
            .Patients.AsNoTracking()
            .Include(p => p.User)
            .Select(p => new ViewPatientDto() {
                Id = p.User.Id,
                FirstName = p.User.FirstName,
                LastName = p.User.LastName,
                BirthDate = p.BirthDate,
                Gender = p.Gender,
                Email = p.User.Email,
                Phone = p.User.PhoneNumber
            })
            .FirstOrDefaultAsync(p => p.Id == id);

        return patient;
    }

    public async Task<string> AddToClinicRecords(AddClinicRecordDto dto) { 
        var clinicRecord = new ClinicRecord();
        clinicRecord.PatientId = Guid.Parse(dto.PatientId);
        clinicRecord.DoctorId = Guid.Parse(dto.DoctorId);
        clinicRecord.Diagnosis = dto.Diagnosis;
        clinicRecord.DoctorNotes = dto.DoctorNotes;
        clinicRecord.VisitDate = dto.VisitDate;
        clinicRecord.FollowUpDate = dto.FollowUpDate;
        
        var prescriptions = new List<Prescription>();

        foreach (var prescriptionData in dto.Prescriptions)
        {
            var prescription = new Prescription();
            prescription.MedicineName = prescriptionData.MedicineName;
            prescription.Dosage = prescriptionData.Dosage;
            prescription.DurationDays = prescriptionData.DurationDays;
            prescription.Frequency = prescriptionData.Frequency;
            prescription.Notes = prescriptionData.Notes;

            prescriptions.Add(prescription);
        }
        clinicRecord.Prescriptions = prescriptions;

        var ctr = await _dbContext.ClinicRecords.AddAsync(clinicRecord);
        await _dbContext.SaveChangesAsync();

        return ctr.Entity.Id.ToString();
    }

    public async Task<List<ClinicRecordDto>> GetAllClinicRecords(string patientId) { 
        return await _dbContext.ClinicRecords
            .Where(cr => cr.PatientId.Equals(Guid.Parse(patientId)))
            .Include(cr => cr.Patient)
            .ThenInclude(p => p.User)
            .Include(cr => cr.Doctor)
            .ThenInclude(dr => dr.User)
            .Include(cr => cr.Prescriptions)
            .AsNoTracking()
            .Select(cr => new ClinicRecordDto() { 
                PatientId = cr.PatientId.ToString(),
                PatientName = $"{cr.Patient.User.FirstName} {cr.Patient.User.LastName}",
                DoctorId = cr.DoctorId.ToString(),
                DoctorName = $"{cr.Doctor.User.FirstName} {cr.Doctor.User.LastName}",
                Diagnosis = cr.Diagnosis,
                DoctorNotes = cr.DoctorNotes,
                VisitDate = cr.VisitDate,
                FollowUpDate = cr.FollowUpDate,
                Prescriptions = cr.Prescriptions.Select(p => new PrescriptionDto()
                {
                    MedicineName = p.MedicineName,
                    Dosage = p.Dosage,
                    DurationDays = p.DurationDays,
                    Frequency = p.Frequency,
                    Notes = p.Notes,
                }).ToList(),
            })
            .ToListAsync();
    }
}
