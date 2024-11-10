using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.Hospital;
using FinalProjectApi.Dtos.HospitalAdmin;
using FinalProjectApi.Dtos.Receptionist;

namespace FinalProjectApi.Interfaces.Services
{
    public interface IHospitalService
    {
        public Task<string> CreateHospital(CreateHospitalDto dto);
        public Task<string> CreateHospitalAdmin(CreateHospitalAdminDtso dto);
        public Task<string> CreateReceptionist(CreateReceptionistDto dto);
        public Task<string> CreateDoctor(CreateDoctorDto dto);
    }
}
