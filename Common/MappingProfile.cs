using AutoMapper;
using FinalProjectApi.Dtos.Appointment;
using FinalProjectApi.Dtos.ClinicRecord;
using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.Hospital;
using FinalProjectApi.Dtos.HospitalAdmin;
using FinalProjectApi.Dtos.Patient;
using FinalProjectApi.Dtos.Receptionist;
using FinalProjectApi.Dtos.User;
using FinalProjectApi.Entities;

namespace FinalProjectApi.Common;

public class MappingProfile: Profile
{
    public MappingProfile()
    {

        //Appointments
        CreateMap<Appointment, ViewAppointmentDto>()
            .ForMember(dest => dest.PatientName, opt => opt.MapFrom(src => $"{src.Patient.User.FirstName} {src.Patient.User.LastName}"))
            .ForMember(dest => dest.PatientPhone, opt => opt.MapFrom(src => src.Patient.User.PhoneNumber))
            .ForMember(dest => dest.PatientEmail, opt => opt.MapFrom(src => src.Patient.User.Email))
            .ForMember(dest => dest.DoctorName, opt => opt.MapFrom(src => $"{src.Doctor.User.FirstName} {src.Doctor.User.LastName}"))
            .ForMember(dest => dest.DoctorSpeciality, opt => opt.MapFrom(src => src.Doctor.Speciality))
            .ForMember(dest => dest.CreatedByUserName, opt => opt.MapFrom(src => $"{src.CreatedByUser.FirstName} {src.CreatedByUser.LastName}"))
            .ForMember(dest => dest.HospitalName, opt => opt.MapFrom(src => src.Hospital.Name))
            .ForMember(dest => dest.HospitalAddress, opt => opt.MapFrom(src => $"{src.Hospital.Street}, {src.Hospital.City}, {src.Hospital.District}"))
            .ForMember(dest => dest.HospitalPhone, opt => opt.MapFrom(src => src.Hospital.Phone));

        CreateMap<ViewAppointmentDto, Appointment>();
        CreateMap<CreateAppointmentDto, Appointment>();

        //User
        CreateMap<User, ViewUserDto>();
        CreateMap<RegisterUserDto, User>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(_ => DateTime.Now))
            .ForMember(dest => dest.UpdatedDate, opt => opt.MapFrom(_ => DateTime.Now));

        //Hospital and Staff
        CreateMap<Doctor, ViewDoctorDto>()
            .ForMember(dest => dest.HospitalName, opt => opt.MapFrom(src => src.Hospital.Name));

        CreateMap<Receptionist, ViewReceptionistDto>()
            .ForMember(dest => dest.HospitalName, opt => opt.MapFrom(src => src.Hospital.Name));

        CreateMap<HospitalAdmin, ViewUserDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.UserId))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.User.FirstName))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.User.LastName))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.User.PhoneNumber))
            .ForMember(dest => dest.HospitalAdminProfile, opt => opt.MapFrom(src => src));

        CreateMap<Doctor, ViewUserDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.UserId))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.User.FirstName))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.User.LastName))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.User.PhoneNumber))
            .ForMember(dest => dest.DoctorProfile, opt => opt.MapFrom(src => src));

        CreateMap<Receptionist, ViewUserDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.UserId))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.User.FirstName))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.User.LastName))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.User.PhoneNumber))
            .ForMember(dest => dest.ReceptionistProfile, opt => opt.MapFrom(src => src));


        CreateMap<Hospital, ViewHospitalDto>()
            .ForMember(dest => dest.Admins, opt => opt.MapFrom(src => src.HospitalAdmins));

        CreateMap<CreateHospitalDto, Hospital>()
            .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(_ => DateTime.Now))
            .ForMember(dest => dest.UpdatedDate, opt => opt.MapFrom(_ => DateTime.Now));

        CreateMap<HospitalAdmin, ViewHospitalAdminDto>()
            .ForMember(dest => dest.HospitalName, opt => opt.MapFrom(src => src.Hospital.Name));

        //Patient
        CreateMap<Patient, ViewPatientDto>();
        CreateMap<AddClinicRecordDto, ClinicRecord>()
            .ForMember(dest => dest.VisitDate, opt => opt.MapFrom(_ => DateTime.UtcNow));
        CreateMap<ClinicRecord, ViewClinicRecordDto>()
            .ForMember(dest => dest.PatientName, opt => opt.MapFrom(src => string.Join(" ", src.Patient.User.FirstName, src.Patient.User.LastName)))
            .ForMember(dest => dest.DoctorName, opt => opt.MapFrom(src => string.Join(" ", src.Doctor.User.FirstName, src.Doctor.User.LastName)));
        CreateMap<AddPrescriptionDto, Prescription>();
        CreateMap<Prescription, ViewPrescriptionDto>()
            .ForMember(dest => dest.IssuedByDoctor, opt => opt.MapFrom(src => $"{src.ClinicRecord.Doctor.User.FirstName} {src.ClinicRecord.Doctor.User.LastName}"))
            .ForMember(dest => dest.IssuedAtHospital, opt => opt.MapFrom(src => src.ClinicRecord.Doctor.Hospital.Name))
            .ForMember(dest => dest.IssuedOn, opt => opt.MapFrom(src => src.ClinicRecord.VisitDate));
    }
}
