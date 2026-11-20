using NexMed360.Application.Access;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.Application.Organizations;

public sealed record UpdatePractitionerBookingDto(bool AcceptsAppointments);

public sealed class PractitionerBookingService(IPractitionerRoleRepository repository, WorkspaceAccess workspace)
{
    private bool CanManage => workspace.Mode == WorkspaceModes.Work && workspace.Has(Permissions.MembersManage);
    public async Task<Result<List<ViewPractitionerRoleDto>>> List(string userId)
    {
        if (!CanManage) return Denied();
        return await repository.Views(await repository.GetForUser(userId));
    }
    public async Task<Result<bool>> Update(Guid id, UpdatePractitionerBookingDto dto)
    {
        if (!CanManage) return Denied();
        var role = await repository.Find(id);
        if (role == null || role.OrganizationId != workspace.OrganizationId)
            return ApiError.From(ErrorCode.SYS_BadRequest, "Practitioner assignment not found.", 404);
        role.AcceptsAppointments = dto.AcceptsAppointments;
        await repository.Save();
        return true;
    }
    private static ApiError Denied() => ApiError.From(ErrorCode.SYS_Unauthorized, "You need permission to manage staff in this organization.", 403);
}
