using NexMed360Api.Common;
using NexMed360Api.Dtos.Doctor;
using NexMed360Api.Dtos.Hospital;
using NexMed360Api.Dtos.HospitalAdmin;
using NexMed360Api.Dtos.Receptionist;
using NexMed360Api.Dtos.User;

namespace NexMed360Api.Interfaces.Services;

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
