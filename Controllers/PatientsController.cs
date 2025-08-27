using FinalProjectApi.Common.ApplicationErrors;
using FinalProjectApi.Common;
using FinalProjectApi.Common.Auth;
using FinalProjectApi.Dtos.ClinicRecord;
using FinalProjectApi.Dtos.Patient;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FinalProjectApi.Dtos.User;

namespace FinalProjectApi.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _patientService;

    public PatientsController(IPatientService patientService)
    {
        _patientService = patientService;
    }

    [HttpPost]
    [Route("CreatePatient")]
    [Authorize(Roles = UserRoles.RECEPTIONIST)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddPatient(CreatePatientDto dto)
    {
        try
        {
            return HandleResult(await _patientService.CreatePatient(dto), true);
        }
        catch (Exception ex)
        {
            return HandleException<ViewUserDto>(ex.Message);
        }
    }

    [HttpGet]
    [Route("ViewPatient/{patientId}")]
    [Authorize(Roles = $"{UserRoles.PATIENT},{UserRoles.RECEPTIONIST},{UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewUserDto>>> ViewPatient(string patientId)
    {
        try
        {
            return HandleResult(await _patientService.ViewPatient(patientId));
        }
        catch (Exception ex)
        {
            return HandleException<ViewUserDto>(ex.Message);
        }
    }

    [HttpPost]
    [Route("AddClinicRecord")]
    [Authorize(Roles = UserRoles.DOCTOR)]
    public async Task<ActionResult<Result<ViewClinicRecordDto>>> AddClinicRecord(AddClinicRecordDto dto)
    {
        try
        {
            var doctorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return HandleResult(await _patientService.AddToClinicRecords(dto, doctorUserId!), true);
        }
        catch (Exception ex)
        {
            return HandleException<ViewClinicRecordDto>(ex.Message);
        }
    }

    [HttpGet]
    [Route("GetClinicRecord")]
    [Authorize(Roles = $"{UserRoles.DOCTOR}, {UserRoles.PATIENT}")]
    [ProducesResponseType(typeof(Result<ViewClinicRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<ViewClinicRecordDto>>> GetClinicRecord([FromQuery] string recordId)
    {
        try
        {
            return HandleResult(await _patientService.ViewClinicRecord(recordId));
        }
        catch (Exception ex)
        {
            return HandleException<ViewClinicRecordDto>(ex.Message);
        }
    }

    [HttpGet]
    [Route("GetClinicRecords")]
    [Authorize(Roles = $"{UserRoles.DOCTOR}, {UserRoles.PATIENT}")]
    [ProducesResponseType(typeof(Result<List<ViewClinicRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<List<ViewClinicRecordDto>>>> GetClinicRecords([FromQuery] string patientId)
    {
        try
        {
            return HandleResult(await _patientService.ViewAllClinicRecords(patientId));
        }
        catch (Exception ex)
        {
            return HandleException<List<ViewClinicRecordDto>>(ex.Message);
        }
    }

    private ActionResult<Result<T>> HandleResult<T>(Result<T> result, bool isCreatedResult = false)
    {
        if (!result.IsSuccess)
            return StatusCode(result.Error!.HttpStatus, result);
        return isCreatedResult ? Created(string.Empty, result) : Ok(result);
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
