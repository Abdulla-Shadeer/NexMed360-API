using NexMed360.Application.Accounts.Internal;
using NexMed360.Application.Shared;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Dtos.User;
using NexMed360Api.Dtos.Overview;
using NexMed360Api.Interfaces.Services;
using NexMed360.Domain.Rules;

namespace NexMed360Api.Services;

public class UserService(IIdentityGateway identity, IOverviewService overviewService, PortalViews views) : IUserService
{
    private readonly IOverviewService _overviewService = overviewService;
    public async Task<Result<ViewUserDto>> GetUserAsync(string id)
    {
        var result = await identity.GetUserAsync(id);
        return result.IsSuccess ? Result<ViewUserDto>.Success(await views.User(result.Data!)) : Result<ViewUserDto>.Failure(result.Error!);
    }
    public async Task<Result<ViewUserDto>> UpdateUser(UpdateUserDto dto, string? requesterUserId)
    {
        if (string.IsNullOrWhiteSpace(requesterUserId))
            return Result<ViewUserDto>.Failure(ErrorCode.SYS_Unauthorized);
        if (!AccountRules.IsSelf(dto.Id, requesterUserId))
            return Result<ViewUserDto>.Failure(ErrorCode.SYS_Unauthorized, "You can update only your own account.", 403);
        var result = await identity.UpdateUser(new AccountChanges(dto.Id, dto.FirstName, dto.LastName, dto.Email, dto.PhoneNumber), requesterUserId);
        return result.IsSuccess ? Result<ViewUserDto>.Success(await views.User(result.Data!)) : Result<ViewUserDto>.Failure(result.Error!);
    }

    public async Task<Result<List<OverviewDto>>> GetOverview(CallerContext user)
    {
        var userId = user.UserId;
        if (userId == null)
            return Result<List<OverviewDto>>.Failure(ErrorCode.SYS_BadRequest, "The received token is missing a valid 'sub' claim");

        var isSuperUser = user.IsInRole(UserRoles.SUPERUSER);
        var isHospitalAdmin = user.IsInRole(UserRoles.HOSPITAL_ADMIN);
        var isReceptionist = user.IsInRole(UserRoles.RECEPTIONIST);
        var isDoctor = user.IsInRole(UserRoles.DOCTOR);
        var isPatient = user.IsInRole(UserRoles.PATIENT);

        List<OverviewDto> overviews = [];

        if (isSuperUser)
            overviews.Add(await _overviewService.GetSystemOverview());

        if (isReceptionist || isHospitalAdmin)
            overviews.Add(await _overviewService.GetHospitalOverview(userId, isHospitalAdmin));

        if (isDoctor)
            overviews.Add(await _overviewService.GetDoctorOverview(userId));

        if (isPatient)
            overviews.Add(await _overviewService.GetPatientOverview(userId));

        return Result<List<OverviewDto>>.Success(overviews);
    }
}
