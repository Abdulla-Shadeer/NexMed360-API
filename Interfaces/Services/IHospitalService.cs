using FinalProjectApi.Common;
using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.Hospital;
using FinalProjectApi.Dtos.HospitalAdmin;
using FinalProjectApi.Dtos.Receptionist;
using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Interfaces.Services;

public interface IHospitalService
{
    public Task<Result<ViewHospitalDto>> CreateHospital(CreateHospitalDto dto);
    public Task<Result<ViewUserDto>> CreateHospitalAdmin(CreateHospitalAdminDto dto);
    public Task<Result<ViewUserDto>> CreateReceptionist(CreateReceptionistDto dto);
    public Task<Result<ViewUserDto>> CreateDoctor(CreateDoctorDto dto);
}
