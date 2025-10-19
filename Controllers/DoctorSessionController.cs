using FinalProjectApi.Common;
using FinalProjectApi.Common.Auth;
using FinalProjectApi.Dtos.doctorSession;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinalProjectApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DoctorSessionController: ControllerBase
{
    private readonly IDoctorSessionService _sessionService;
    public DoctorSessionController(IDoctorSessionService doctorSessionService) { 
        _sessionService = doctorSessionService;
    }

    [HttpPost]
    [Route("Create")]
    [Authorize(Roles = $"{UserRoles.HOSPITAL_ADMIN}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewDoctorSessionDto>>> Create(CreateDoctorSessionDto createSessionDto)
    {
        return HandleResult(await _sessionService.Create(createSessionDto), true);
    }

    [HttpGet]
    [Route("Get")]
    [Authorize(Roles = $"{UserRoles.HOSPITAL_ADMIN}, {UserRoles.DOCTOR}, {UserRoles.PATIENT}")]
    public async Task<ActionResult<Result<List<ViewDoctorSessionDto>>>> Get([FromQuery] FilterDoctorSessionDto filter)
    {
        return HandleResult(await _sessionService.Get(filter));
    }

    [HttpGet]
    [Route("GetSlots")]
    [Authorize(Roles = $"{UserRoles.HOSPITAL_ADMIN}, {UserRoles.DOCTOR}, {UserRoles.PATIENT}")]
    public async Task<ActionResult<Result<List<ViewSessionSlotDto>>>> GetSlots([FromQuery] GetSessionSlotsDto dto)
    {
        return HandleResult(await _sessionService.GetSlots(dto));
    }

    [HttpGet]
    [Route("Start/{sessionId}")]
    [Authorize(Roles = UserRoles.DOCTOR)]
    public async Task<ActionResult<Result<ViewSessionHistoryDto>>> Start(string sessionId)
    { 
        return HandleResult(await _sessionService.Start(sessionId));
    }

    [HttpPost]
    [Route("UpdateHistory")]
    [Authorize(Roles = UserRoles.DOCTOR)]
    public async Task<ActionResult<Result<ViewSessionHistoryDto>>> UpdateHistory(UpdateSessionHistoryDto dto)
    {
        return HandleResult(await _sessionService.UpdateHistory(dto));
    }

    [HttpGet]
    [Route("GetHistory")]
    [Authorize(Roles = UserRoles.DOCTOR)]
    public async Task<ActionResult<Result<List<ViewSessionHistoryDto>>>> GetHistory([FromQuery] FilterSessionHistoryDto filter)
    {
        return HandleResult(await _sessionService.GetHistory(filter));
    }

    [HttpGet]
    [Route("GetCurrentOrUpcoming")]
    [Authorize(Roles = UserRoles.DOCTOR)]
    public async Task<ActionResult<Result<ViewCurrentOrNextSessionDto>>> GetCurrentOrUpcoming([FromQuery] FilterSessionsForCurrentDayDto filter)
    {
        return HandleResult(await _sessionService.GetCurrentOrUpcoming(filter));
    }

    [HttpGet]
    [Route("HasAnySessionToday")]
    [Authorize(Roles = UserRoles.DOCTOR)]
    public ActionResult<Result<bool>> HasAnySessionToday([FromQuery] FilterSessionsForCurrentDayDto filter) 
    {
        return HandleResult(_sessionService.HasAnySessionToday(filter));
    }

    [HttpGet]
    [Route("GetOngoing")]
    [Authorize(Roles = UserRoles.RECEPTIONIST)]
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
