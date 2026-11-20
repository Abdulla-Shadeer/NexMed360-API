using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexMed360.Application.Organizations;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;

namespace NexMed360Api.Controllers.v1;

[ApiController, ApiVersion(1.0), Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class LocationsController(LocationService service) : ControllerBase
{
    [HttpGet, Workspace(WorkspaceModes.Work, Permissions.OrganizationsManage)]
    public async Task<ActionResult<Result<List<ManagedLocationDto>>>> List() => Respond(await service.List());
    [HttpPost, Workspace(WorkspaceModes.Work, Permissions.OrganizationsManage)]
    public async Task<ActionResult<Result<ManagedLocationDto>>> Create(SaveLocationDto dto) => Respond(await service.Save(null, dto));
    [HttpPut("{id:guid}"), Workspace(WorkspaceModes.Work, Permissions.OrganizationsManage)]
    public async Task<ActionResult<Result<ManagedLocationDto>>> Update(Guid id, SaveLocationDto dto) => Respond(await service.Save(id, dto));
    private ActionResult<Result<T>> Respond<T>(Result<T> result) => result.IsSuccess ? Ok(result) : StatusCode(result.Error!.HttpStatus, result);
}
