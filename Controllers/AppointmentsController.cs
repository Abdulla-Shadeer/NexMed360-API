using FinalProjectApi.Common;
using FinalProjectApi.Common.Auth;
using FinalProjectApi.Dtos.Appointment;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<ActionResult<Result<string>>> CreateAppointment(CreateAppointment createAppointment)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _appointmentsService.CreateAppointment(createAppointment);
            if (!result.IsSuccess)
                StatusCode(result.Error!.HttpStatus, result);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
        }
    }

    [HttpGet]
    [Route("View")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<List<ViewAppointment>>>> ViewAppointments([FromQuery] ViewAppointmentsFilter filter)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _appointmentsService.ViewAppointments(filter);
            if (!result.IsSuccess)
                return StatusCode(result.Error!.HttpStatus, result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
        }
    }

    [HttpPost]
    [Route("Edit")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointment>>> ModifyAppointment(ModifyAppointmentDto modifyAppointmentDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _appointmentsService.ModifyAppointment(modifyAppointmentDto);
            if (!result.IsSuccess)
                return StatusCode(result.Error!.HttpStatus, result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
        }
    }

    [HttpGet]
    [Route("View/{appointmentId}")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewAppointment>>> ViewAppointment(string appointmentId)
    {
        try
        {
            var result = await _appointmentsService.ViewAppointment(appointmentId);
            if (!result.IsSuccess)
                return StatusCode(result.Error!.HttpStatus, result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
        }
    }
}
