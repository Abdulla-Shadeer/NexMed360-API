using FinalProjectApi.Common;
using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.Hospital;
using FinalProjectApi.Dtos.HospitalAdmin;
using FinalProjectApi.Dtos.Receptionist;
using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Interfaces.Services;

public interface IHospitalService
{
    public Task<Result<ViewHospitalDto>> Add(CreateHospitalDto dto);
    public Task<Result<ViewUserDto>> AssignAdmin(CreateHospitalAdminDto dto);
    public Task<Result<ViewUserDto>> AddReceptionist(CreateReceptionistDto dto);
    public Task<Result<ViewUserDto>> AddDoctor(CreateDoctorDto dto);
    public Task<Result<List<ViewHospitalDto>>> GetAll();
    public Task<Result<ViewHospitalDto>> Update(UpdateHospitalDto updateDto);
    public Task<Result<ViewHospitalDto>> FindById(string id, bool isSuperUser);
    public Task<Result<List<ViewUserDto>>> GetMembers(string hospitalId, string userId);
}
