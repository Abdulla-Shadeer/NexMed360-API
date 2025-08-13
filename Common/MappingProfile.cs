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
    public MappingProfile() {

        //Appointments
        CreateMap<Appointment, ViewAppointmentDto>();
        CreateMap<ViewAppointmentDto, Appointment>();

        //User
        CreateMap<User, ViewUserDto>();
        CreateMap<RegisterUserDto, User>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(_ => DateTime.Now))
            .ForMember(dest => dest.UpdatedDate, opt => opt.MapFrom(_ => DateTime.Now));

        //Hospital and Staff
        CreateMap<Doctor, ViewDoctorDto>();
        CreateMap<Receptionist, ViewReceptionistDto>();
        CreateMap<Hospital, ViewHospitalDto>();
        CreateMap<CreateHospitalDto, Hospital>()
            .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(_ => DateTime.Now))
            .ForMember(dest => dest.UpdatedDate, opt => opt.MapFrom(_ => DateTime.Now));
        CreateMap<HospitalAdmin, ViewHospitalAdminDto>();

        //Patient
        CreateMap<Patient, ViewPatientDto>();
        CreateMap<AddClinicRecordDto, ClinicRecord>();
        CreateMap<ClinicRecord, ViewClinicRecordDto>()
            .ForMember(dest => dest.PatientName, opt => opt.MapFrom(src => string.Join(" ", src.Patient.User.FirstName, src.Patient.User.LastName)))
            .ForMember(dest => dest.DoctorName, opt => opt.MapFrom(src => string.Join(" ", src.Doctor.User.FirstName, src.Doctor.User.LastName)));
        CreateMap<AddPrescriptionDto, Prescription>();
        CreateMap<Prescription, ViewPrescriptionDto>();
    }
}
