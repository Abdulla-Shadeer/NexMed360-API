using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexMed360.Application.Access;
using NexMed360.Application.Patients;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;

namespace NexMed360Api.Controllers.v1;

[ApiController, ApiVersion(1.0), Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RecordSharingController(RecordSharingService service) : ControllerBase
{
    [HttpGet, Workspace(WorkspaceModes.Work + "," + WorkspaceModes.Patient, Permissions.ClinicalRecordsRead)]
    public async Task<ActionResult<Result<List<RecordSharingRequestDto>>>> List([FromQuery] Guid? patientId) =>
        Respond(await service.List(patientId, User.FindFirstValue(ClaimTypes.NameIdentifier)));

    [HttpPost, Workspace(WorkspaceModes.Work, Permissions.ClinicalRecordsRead, true)]
    public async Task<ActionResult<Result<Guid>>> Create(CreateRecordSharingRequestDto dto) =>
        Respond(await service.Create(dto, User.FindFirstValue(ClaimTypes.NameIdentifier)));

    [HttpPost("{id:guid}/Decision"), Workspace(WorkspaceModes.Patient)]
    public async Task<ActionResult<Result<bool>>> Decide(Guid id, DecideRecordSharingRequestDto dto) =>
        Respond(await service.Decide(id, dto.Approve, User.FindFirstValue(ClaimTypes.NameIdentifier)));

    [HttpPost("{id:guid}/Revoke"), Workspace(WorkspaceModes.Patient)]
    public async Task<ActionResult<Result<bool>>> Revoke(Guid id) =>
        Respond(await service.Revoke(id, User.FindFirstValue(ClaimTypes.NameIdentifier)));

    private ActionResult<Result<T>> Respond<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result) : StatusCode(result.Error!.HttpStatus, result);
}
