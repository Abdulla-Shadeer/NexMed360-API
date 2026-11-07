using Asp.Versioning;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.practitionerSchedule;
using NexMed360Api.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NexMed360.API.Dtos.practitionerSchedule;

namespace NexMed360Api.Controllers.v1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public class PractitionerScheduleController: ControllerBase
{
    private readonly IPractitionerScheduleService _sessionService;
    public PractitionerScheduleController(IPractitionerScheduleService practitionerScheduleService) { 
        _sessionService = practitionerScheduleService;
    }

    [HttpPost]
    [Route("Create")]
    [Authorize(Roles = $"{UserRoles.ORGANIZATION_MANAGER}, {UserRoles.PRACTITIONER}")]
    public async Task<ActionResult<Result<ViewPractitionerScheduleDto>>> Create(CreatePractitionerScheduleDto createSessionDto)
    {
        return HandleResult(await _sessionService.Create(createSessionDto), true);
    }

    [HttpGet]
    [Route("Get")]
    [Authorize(Roles = $"{UserRoles.ORGANIZATION_MANAGER}, {UserRoles.PRACTITIONER}, {UserRoles.PATIENT}")]
    public async Task<ActionResult<Result<List<ViewPractitionerScheduleDto>>>> Get([FromQuery] FilterPractitionerScheduleDto filter)
    {
        return HandleResult(await _sessionService.Get(filter));
    }

    [HttpPut]
    [Route("Update/{sessionId}")]
    [Authorize(Roles = $"{UserRoles.ORGANIZATION_MANAGER}, {UserRoles.PRACTITIONER}")]
    public async Task<ActionResult<Result<ViewPractitionerScheduleDto>>> Update(string sessionId, UpdatePractitionerScheduleDto updateSessionDto)
    {
        return HandleResult(await _sessionService.Update(sessionId, updateSessionDto));
    }

    [HttpDelete]
    [Route("Delete/{sessionId}")]
    [Authorize(Roles = $"{UserRoles.ORGANIZATION_MANAGER}, {UserRoles.PRACTITIONER}")]
    public async Task<ActionResult<Result<string>>> Delete(string sessionId)
    {
        return HandleResult(await _sessionService.Delete(sessionId));
    }

    [HttpGet]
    [Route("GetSlots")]
    [Authorize(Roles = $"{UserRoles.ORGANIZATION_MANAGER}, {UserRoles.PRACTITIONER}, {UserRoles.PATIENT}")]
    public async Task<ActionResult<Result<List<ViewSessionSlotDto>>>> GetSlots([FromQuery] GetSessionSlotsDto dto)
    {
        return HandleResult(await _sessionService.GetSlots(dto));
    }

    [HttpGet]
    [Route("Start/{sessionId}")]
    [Authorize(Roles = UserRoles.PRACTITIONER)]
    public async Task<ActionResult<Result<ViewSessionHistoryDto>>> Start(string sessionId)
    { 
        return HandleResult(await _sessionService.Start(sessionId));
    }

    [HttpPost]
    [Route("UpdateHistory")]
    [Authorize(Roles = UserRoles.PRACTITIONER)]
    public async Task<ActionResult<Result<ViewSessionHistoryDto>>> UpdateHistory(UpdateSessionHistoryDto dto)
    {
        return HandleResult(await _sessionService.UpdateHistory(dto));
    }

    [HttpGet]
    [Route("GetHistory")]
    [Authorize(Roles = UserRoles.PRACTITIONER)]
    public async Task<ActionResult<Result<List<ViewSessionHistoryDto>>>> GetHistory([FromQuery] FilterSessionHistoryDto filter)
    {
        return HandleResult(await _sessionService.GetHistory(filter));
    }

    [HttpGet]
    [Route("GetCurrentOrUpcoming")]
    [Authorize(Roles = UserRoles.PRACTITIONER)]
    public async Task<ActionResult<Result<ViewCurrentOrNextSessionDto>>> GetCurrentOrUpcoming([FromQuery] FilterSessionsForCurrentDayDto filter)
    {
        return HandleResult(await _sessionService.GetCurrentOrUpcoming(filter));
    }

    [HttpGet]
    [Route("HasAnySessionToday")]
    [Authorize(Roles = UserRoles.PRACTITIONER)]
    public ActionResult<Result<bool>> HasAnySessionToday([FromQuery] FilterSessionsForCurrentDayDto filter) 
    {
        return HandleResult(_sessionService.HasAnySessionToday(filter));
    }

    [HttpGet]
    [Route("GetOngoing")]
    [Authorize(Roles = UserRoles.ORGANIZATION_MEMBER)]
    public async Task<ActionResult<Result<List<ViewSessionHistoryDto>>>> GetOngoing()
    {
        return HandleResult(await _sessionService.GetOngoing(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
    }

    private ActionResult<Result<T>> HandleResult<T>(Result<T> result, bool isCreationResult = false)
    {
        if (!result.IsSuccess)
            return StatusCode(result.Error!.HttpStatus, result);
        return isCreationResult ? Created(string.Empty, result) : Ok(result);
    }
}
