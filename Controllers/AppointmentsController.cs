using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
using FinalProjectApi.Common.Auth;
using FinalProjectApi.Dtos.Appointment;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinalProjectApi.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentsService;

    public AppointmentsController(IAppointmentService appointmentsService)
    {
        _appointmentsService = appointmentsService;
    }

    [HttpPost]
    [Route("Create")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> CreateAppointment(CreateAppointmentDto createAppointment)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return HandleResult(await _appointmentsService.CreateAppointment(createAppointment, requesterId!), true);
        }
        catch (Exception ex)
        {
            return HandleException<ViewAppointmentDto>(ex.Message);
        }
    }

    [HttpGet]
    [Route("View")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<List<ViewAppointmentDto>>>> ViewAppointments([FromQuery] ViewAppointmentsFilterDto filter)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            return HandleResult(await _appointmentsService.ViewAppointments(filter));
        }
        catch (Exception ex)
        {
            return HandleException<List<ViewAppointmentDto>>(ex.Message);
        }
    }

    [HttpPost]
    [Route("Edit")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ModifyAppointment(ModifyAppointmentDto modifyAppointmentDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return HandleResult(await _appointmentsService.ModifyAppointment(modifyAppointmentDto, requesterId!));
        }
        catch (Exception ex)
        {
            return HandleException<ViewAppointmentDto>(ex.Message);
        }
    }

    [HttpGet]
    [Route("View/{appointmentId}")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ViewAppointment(string appointmentId)
    {
        try
        {
            return HandleResult(await _appointmentsService.ViewAppointment(appointmentId));
        }
        catch (Exception ex)
        {
            return HandleException<ViewAppointmentDto>(ex.Message);
        }
    }

    [HttpGet]
    [Route("Confirm/{appointmentId}")]
    [Authorize(Roles = $"{UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> ConfirmAppointment(string appointmentId)
    {
        try
        {
            return HandleResult(await _appointmentsService.ConfirmOrCompleteAppointment(appointmentId, false));
        }
        catch (Exception ex)
        {
            return HandleException<ViewAppointmentDto>(ex.Message);
        }
    }

    [HttpGet]
    [Route("Complete/{appointmentId}")]
    [Authorize(Roles = $"{UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> CompleteAppointment(string appointmentId)
    {
        try
        {
            return HandleResult(await _appointmentsService.ConfirmOrCompleteAppointment(appointmentId, true));
        }
        catch (Exception ex)
        {
            return HandleException<ViewAppointmentDto>(ex.Message);
        }
    }

    [HttpPost]
    [Route("Cancel")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointmentDto>>> CancelAppointment(CancellationRequestDto cancellationRequestDto)
    {
        if(!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return HandleResult(await _appointmentsService.CancelAppointment(cancellationRequestDto, requesterId!));
        }
        catch (Exception ex)
        {
            return HandleException<ViewAppointmentDto>(ex.Message);
        }
    }

    private ActionResult<Result<T>> HandleResult<T>(Result<T> result, bool isCreationResult = false)
    {
        if (!result.IsSuccess)
            return StatusCode(result.Error!.HttpStatus, result);
        return isCreationResult ? Created(string.Empty, result) : Ok(result);
    }

    private ActionResult<Result<T>> HandleException<T>(string description)
    {
        return StatusCode(
            500,
            Result<T>.Failure(
                new ApiError(ErrorCode.Internal, $"An error occurred while processing the request.", 500, description)
            )
        );
    }
}
