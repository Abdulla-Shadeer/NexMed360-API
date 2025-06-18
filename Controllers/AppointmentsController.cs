using FinalProjectApi.Common.Auth;
using FinalProjectApi.Common.Exceptions;
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

    public AppointmentsController(IAppointmentService appointmentsService) { 
        _appointmentsService = appointmentsService;
    }

    [HttpPost]
    [Route("Create")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<string>> CreateAppointment(CreateAppointment createAppointment) {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        
        try
        {
            var appointmentId = await _appointmentsService.CreateAppointment(createAppointment);
            return Created("", new { BookingRef = appointmentId });
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) {
            return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
        }
    }

    [HttpGet]
    [Route("View")]
    [Authorize(Roles = $"{UserRoles.PATIENT}, {UserRoles.RECEPTIONIST}, {UserRoles.DOCTOR}")]
    public async Task<ActionResult<List<ViewAppointment>>> ViewAppointments([FromQuery] ViewAppointmentsFilter filter) {
        try
        {
            return Ok(await _appointmentsService.ViewAppointments(filter));
        }
        catch (BadRequestException ex) { 
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
        }
    }
}
