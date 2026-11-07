using NexMed360.Application.Shared;
using NexMed360Api.Entities;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.Doctor;
using NexMed360Api.Dtos.Hospital;
using NexMed360Api.Dtos.HospitalAdmin;
using NexMed360Api.Dtos.Receptionist;
using NexMed360Api.Dtos.User;
using NexMed360Api.Interfaces.Services;
using System.Text.Json;
namespace NexMed360Api.Services;

public class HospitalService(IHospitalRepository data, IAuthService auth, IRegistrationTransactions transactions, PortalViews views, IDoctorRepository doctors, IHospitalAdminRepository admins, IReceptionistRepository receptionists) : IHospitalService
{
    public async Task<Result<ViewUserDto>> AssignAdmin(CreateHospitalAdminDto dto)
    {
        if (!Guid.TryParse(dto.HospitalId, out var hospitalId)) return Result<ViewUserDto>.Failure(ErrorCode.SYS_InvalidGuid);
        if (!await data.Exists(hospitalId)) return Result<ViewUserDto>.Failure(ErrorCode.HOS_NotFound);
        await using var transaction = await transactions.BeginAsync();
        var registered = await auth.RegisterUser(dto.PersonalInformation, UserRoles.HOSPITAL_ADMIN);
        if (!registered.IsSuccess) return registered;
        registered.Data!.HospitalAdminProfile = await views.Admin(await admins.Add(new HospitalAdmin { HospitalId = hospitalId, UserId = registered.Data.Id, CreatedDate = DateTime.Now, UpdatedDate = DateTime.Now }));
        await transaction.CommitAsync();
        return registered;
    }
    public async Task<Result<ViewUserDto>> AddReceptionist(CreateReceptionistDto dto)
    {
        if (!Guid.TryParse(dto.HospitalId, out var hospitalId)) return Result<ViewUserDto>.Failure(ErrorCode.SYS_InvalidGuid);
        if (!await data.Exists(hospitalId)) return Result<ViewUserDto>.Failure(ErrorCode.HOS_NotFound);
        await using var transaction = await transactions.BeginAsync();
        var registered = await auth.RegisterUser(dto.PersonalInformation, UserRoles.RECEPTIONIST);
        if (!registered.IsSuccess) return registered;
        registered.Data!.ReceptionistProfile = await views.Receptionist(await receptionists.Add(new Receptionist { HospitalId = hospitalId, UserId = registered.Data.Id, Department = dto.Department, Location = dto.Location, CreatedDate = DateTime.Now, UpdatedDate = DateTime.Now }));
        await transaction.CommitAsync();
        return registered;
    }
    public async Task<Result<ViewUserDto>> AddDoctor(CreateDoctorDto dto)
    {
        if (!Guid.TryParse(dto.HospitalId, out var hospitalId)) return Result<ViewUserDto>.Failure(ErrorCode.SYS_InvalidGuid);
        if (!await data.Exists(hospitalId)) return Result<ViewUserDto>.Failure(ErrorCode.HOS_NotFound);
        await using var transaction = await transactions.BeginAsync();
        var registered = await auth.RegisterUser(dto.PersonalInformation, UserRoles.DOCTOR);
        if (!registered.IsSuccess) return registered;
        registered.Data!.DoctorProfile = await views.Doctor(await doctors.Add(new Doctor { HospitalId = hospitalId, UserId = registered.Data.Id, Speciality = dto.Speciality, CreatedDate = DateTime.Now, UpdatedDate = DateTime.Now }));
        await transaction.CommitAsync();
        return registered;
    }
    public async Task<Result<ViewHospitalDto>> Add(CreateHospitalDto dto) => Result<ViewHospitalDto>.Success(await views.Hospital(await data.Add(new Hospital { Name = dto.Name, Street = dto.Street, City = dto.City, District = dto.District, Phone = dto.Phone, Grade = dto.Grade, CreatedDate = DateTime.Now, UpdatedDate = DateTime.Now })));
    public async Task<Result<List<ViewHospitalDto>>> GetAll() => Result<List<ViewHospitalDto>>.Success(await views.Hospitals(await data.GetAll()));
    public async Task<Result<ViewHospitalDto>> Update(UpdateHospitalDto dto)
    {
        if (!Guid.TryParse(dto.Id, out var id)) return Result<ViewHospitalDto>.Failure(ErrorCode.SYS_InvalidGuid, "Hospital ID has invalid format!");
        var hospital = await data.Find(id);
        if (hospital == null) return Result<ViewHospitalDto>.Failure(ErrorCode.HOS_NotFound);
        hospital.Update(dto.Name, dto.Street, dto.City, dto.District, dto.Phone, dto.Grade);
        return Result<ViewHospitalDto>.Success(await views.Hospital(await data.Save(hospital)));
    }
    public async Task<Result<ViewHospitalDto>> FindById(string id, bool isSuperUser)
    {
        if (!Guid.TryParse(id, out var hospitalId)) return Result<ViewHospitalDto>.Failure(ErrorCode.SYS_InvalidGuid, "Hospital ID has invalid format!");
        return Result<ViewHospitalDto>.Success(await views.Hospital(await data.Find(hospitalId), isSuperUser));
    }
    public async Task<Result<List<ViewUserDto>>> GetMembers(string id, string userId)
    {
        if (!Guid.TryParse(id, out var hospitalId)) return Result<List<ViewUserDto>>.Failure(ErrorCode.SYS_InvalidGuid, "Invalid hospital ID format.");
        if (!await data.Exists(hospitalId)) return Result<List<ViewUserDto>>.Failure(ErrorCode.HOS_NotFound, $"Hospital with ID {id} not found!");
        if (!await data.IsAdmin(hospitalId, userId)) return Result<List<ViewUserDto>>.Failure(ErrorCode.SYS_Unauthorized, "Access denied, only admins of this hospital can view its members.");
        var rows = await data.GetMembers(hospitalId);
        return Result<List<ViewUserDto>>.Success(await views.Members(rows.Doctors, rows.Receptionists));
    }
}
