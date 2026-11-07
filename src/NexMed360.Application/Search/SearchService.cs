using NexMed360.Application.Search;
using NexMed360.Application.Shared;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Dtos.Search;
using NexMed360Api.Dtos.User;
using NexMed360Api.Dtos.Doctor;
using NexMed360Api.Interfaces.Services;

namespace NexMed360Api.Services;
public class SearchService(ISearchData data, IUserService users, PortalViews views) : ISearchService
{
    private readonly List<KeyValuePair<EntityTypes, List<string>>> requiredRolesToSearch = [
        new(EntityTypes.Patient, [UserRoles.DOCTOR, UserRoles.RECEPTIONIST]),
        new(EntityTypes.Doctor, [UserRoles.PATIENT, UserRoles.HOSPITAL_ADMIN, UserRoles.RECEPTIONIST, UserRoles.DOCTOR, UserRoles.SUPERUSER]),
        new(EntityTypes.Hospital, [UserRoles.PATIENT, UserRoles.HOSPITAL_ADMIN, UserRoles.RECEPTIONIST, UserRoles.DOCTOR, UserRoles.SUPERUSER])
        ];
    public async Task<Result<object>> EntitySearch(EntitySearchDto dto, CallerContext user)
    {
        // user has roles
        // each entity search require specific roles
        // search operation should detect the requested entity and its role requirements
        // check if user has any of those required roles
        // return result if ok, else return unauthorized
        if (requiredRolesToSearch.FirstOrDefault(item => item.Key == dto.Entity).Value.Any(role => user.IsInRole(role))) {
            return dto.Entity switch
            {
                EntityTypes.Patient => Result<object>.Success(await Users(await data.PatientUserIds(dto.InputType, dto.Input))),
                EntityTypes.Doctor => Result<object>.Success(await Users(await data.DoctorUserIds(dto.InputType, dto.Input))),
                EntityTypes.Hospital => Result<object>.Success(await views.Hospitals(await data.Hospitals(dto.Input))),
                _ => Result<object>.Failure(new ApiError(ErrorCode.SYS_BadRequest, $"Unknown Entity type \"{dto.Entity}\"", 400)),
            };
        }
        return Result<object>.Failure(new ApiError(ErrorCode.SYS_Unauthorized, $"You're not authorized to view {dto.Entity} data.", 401));
    }
    private async Task<List<ViewUserDto>> Users(List<string> ids)
    {
        var rows = new List<ViewUserDto>(); foreach (var id in ids) rows.Add((await users.GetUserAsync(id)).Data!); return rows;
    }
    public async Task<Result<List<ViewDoctorDto>>> SearchDoctor(SearchDoctorDto dto)
    {
        if (String.IsNullOrEmpty(dto.Name) && String.IsNullOrEmpty(dto.Email) && String.IsNullOrEmpty(dto.Speciality))
            return Result<List<ViewDoctorDto>>.Failure(ErrorCode.SYS_BadRequest, "Empty filter!");
        return Result<List<ViewDoctorDto>>.Success(await views.Doctors(await data.Doctors(dto.Name, dto.Email, dto.Speciality)));
    }
}
