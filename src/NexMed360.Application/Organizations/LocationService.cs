using NexMed360.Application.Access;
using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;
using NexMed360Api.Common.Auth;
using NexMed360Api.Entities;

namespace NexMed360.Application.Organizations;

public sealed class LocationService(ILocationRepository repository, WorkspaceAccess workspace)
{
    private bool CanManage => workspace.Mode == WorkspaceModes.Work && workspace.OrganizationId.HasValue
        && workspace.Has(Permissions.OrganizationsManage) && !workspace.HasLocationScope;

    public async Task<Result<List<ManagedLocationDto>>> List()
    {
        if (!CanManage) return Denied();
        return (await repository.List()).Select(View).ToList();
    }

    public async Task<Result<ManagedLocationDto>> Save(Guid? id, SaveLocationDto dto)
    {
        if (!CanManage) return Denied();
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 150)
            return ApiError.From(ErrorCode.SYS_BadRequest, "Enter a location name of up to 150 characters.");
        if (!Enum.TryParse<LocationType>(dto.Type, out var type) || !Enum.IsDefined(type))
            return ApiError.From(ErrorCode.SYS_BadRequest, "Choose a location type from the list.");
        var location = id.HasValue ? await repository.Find(id.Value) : new Location { OrganizationId = workspace.OrganizationId!.Value };
        if (location == null || location.OrganizationId != workspace.OrganizationId)
            return ApiError.From(ErrorCode.SYS_BadRequest, "Location not found.", 404);

        var locations = (await repository.List()).ToDictionary(row => row.Id);
        var parentId = dto.ParentId;
        var visited = new HashSet<Guid> { location.Id };
        // A parent must stay in this organization and cannot be its own descendant.
        while (parentId.HasValue)
        {
            if (!visited.Add(parentId.Value) || !locations.TryGetValue(parentId.Value, out var parent))
                return ApiError.From(ErrorCode.SYS_BadRequest, "Choose a parent location in this organization without a cycle.");
            if (dto.IsActive && !parent.IsActive)
                return ApiError.From(ErrorCode.SYS_BadRequest, "An active location needs active parent locations.");
            parentId = parent.ParentId;
        }
        if (!dto.IsActive && locations.Values.Any(row => row.ParentId == location.Id && row.IsActive))
            return ApiError.From(ErrorCode.SYS_BadRequest, "Deactivate the child locations first.");

        location.Name = dto.Name.Trim(); location.Type = type; location.ParentId = dto.ParentId;
        location.Address = dto.Address?.Trim(); location.IsActive = dto.IsActive; location.UpdatedDate = DateTime.UtcNow;
        await repository.Save(location, !id.HasValue);
        return View(location);
    }

    private static ManagedLocationDto View(Location row) => new(row.Id, row.Name, row.Type.ToString(), row.ParentId, row.Address, row.IsActive);
    private static ApiError Denied() => ApiError.From(ErrorCode.SYS_Unauthorized, "Choose an unrestricted organization workspace with permission to manage organization details.", 403);
}
