using NexMed360Api.Common.Auth;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexMed360.Application.Access;
using NexMed360Api.Common;

namespace NexMed360Api.Controllers.v1;

[ApiController, ApiVersion(1.0), Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class OrganizationAdministrationController(AccessService service) : ControllerBase
{
    [HttpGet("{organizationId:guid}/Administrators"), Workspace(WorkspaceModes.Platform)]
    public async Task<ActionResult<Result<OrganizationAdministratorsDto>>> Administrators(Guid organizationId) =>
        Handle(await service.OrganizationAdministrators(organizationId));

    [HttpPut("{organizationId:guid}/Administrators/{membershipId:guid}"), Workspace(WorkspaceModes.Platform)]
    public async Task<ActionResult<Result<string>>> UpdateAdministrator(Guid organizationId, Guid membershipId, UpdateOrganizationAdministratorDto dto) =>
        Handle(await service.UpdateAdministrator(organizationId, membershipId, dto));

    [HttpDelete("{organizationId:guid}/Administrators/{membershipId:guid}"), Workspace(WorkspaceModes.Platform)]
    public async Task<ActionResult<Result<string>>> RemoveAdministrator(Guid organizationId, Guid membershipId) =>
        Handle(await service.RemoveAdministrator(organizationId, membershipId));

    [HttpDelete("{organizationId:guid}"), Workspace(WorkspaceModes.Platform)]
    public async Task<ActionResult<Result<string>>> DeleteOrganization(Guid organizationId) =>
        Handle(await service.DeleteOrganization(organizationId));

    private ActionResult<Result<T>> Handle<T>(Result<T> result) => StatusCode(result.IsSuccess ? 200 : result.Error!.HttpStatus, result);
}
