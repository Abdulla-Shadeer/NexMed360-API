using FinalProjectApi.Common;
using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.Hospital;
using FinalProjectApi.Dtos.HospitalAdmin;
using FinalProjectApi.Dtos.Receptionist;

namespace FinalProjectApi.Interfaces.Services
{
    public interface IHospitalService
    {
        public Task<Result<ViewHospitalDto>> CreateHospital(CreateHospitalDto dto);
        public Task<Result<ViewHospitalAdminDto>> CreateHospitalAdmin(CreateHospitalAdminDto dto);
        public Task<Result<ViewReceptionistDto>> CreateReceptionist(CreateReceptionistDto dto);
        public Task<Result<ViewDoctorDto>> CreateDoctor(CreateDoctorDto dto);
    }
}
