using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexMed360.Application.Organizations;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.PractitionerRole;

namespace NexMed360Api.Controllers.v1;

[ApiController, ApiVersion(1.0), Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class PractitionerAssignmentsController(PractitionerBookingService service) : ControllerBase
{
    [HttpGet, Workspace(WorkspaceModes.Work, Permissions.MembersManage)]
    public async Task<ActionResult<Result<List<ViewPractitionerRoleDto>>>> List([FromQuery] string userId) => Respond(await service.List(userId));
    [HttpPut("{id:guid}/Booking"), Workspace(WorkspaceModes.Work, Permissions.MembersManage)]
    public async Task<ActionResult<Result<bool>>> UpdateBooking(Guid id, UpdatePractitionerBookingDto dto) => Respond(await service.Update(id, dto));
    private ActionResult<Result<T>> Respond<T>(Result<T> result) => result.IsSuccess ? Ok(result) : StatusCode(result.Error!.HttpStatus, result);
}
