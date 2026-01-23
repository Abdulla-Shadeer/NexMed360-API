using Asp.Versioning;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.ClinicRecord;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NexMed360Api.Controllers.v1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public class PatientController : ControllerBase
{
    private readonly IPatientService _patientService;

    public PatientController(IPatientService patientService)
    {
        _patientService = patientService;
    }

    [HttpPost]
    [Route("Create")]
    [Authorize(Roles = UserRoles.RECEPTIONIST)]
    public async Task<ActionResult<Result<ViewPatientDto>>> Create(CreatePatientDto dto)
    {
        return HandleResult(await _patientService.CreatePatient(dto), true);
    }

    [HttpGet]
    [Route("Get/{patientId}")]
    [Authorize(Roles = $"{UserRoles.RECEPTIONIST},{UserRoles.DOCTOR}")]
    public async Task<ActionResult<Result<ViewPatientDto>>> GetPatient(string patientId)
    {
        return HandleResult(await _patientService.GetPatient(patientId));
    }

    [HttpPost]
    [Route("AddClinicRecord")]
    [Authorize(Roles = UserRoles.DOCTOR)]
    public async Task<ActionResult<Result<ViewClinicRecordDto>>> AddClinicRecord(AddClinicRecordDto dto)
    {
        var doctorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _patientService.AddToClinicRecords(dto, doctorUserId!), true);
    }

    [HttpGet]
    [Route("GetClinicRecord/{recordId}")]
    [Authorize(Roles = $"{UserRoles.DOCTOR}, {UserRoles.PATIENT}")]
    [ProducesResponseType(typeof(Result<ViewClinicRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<ViewClinicRecordDto>>> GetClinicRecord(string recordId)
    {
        return HandleResult(await _patientService.ViewClinicRecord(recordId));
    }

    [HttpGet]
    [Route("GetClinicRecords/{patientId}")]
    [Authorize(Roles = $"{UserRoles.DOCTOR}, {UserRoles.PATIENT}")]
    [ProducesResponseType(typeof(Result<List<ViewClinicRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<List<ViewClinicRecordDto>>>> GetClinicRecords(string patientId)
    {
        return HandleResult(await _patientService.ViewAllClinicRecords(patientId));
    }

    private ActionResult<Result<T>> HandleResult<T>(Result<T> result, bool isCreatedResult = false)
    {
        if (!result.IsSuccess)
            return StatusCode(result.Error!.HttpStatus, result);
        return isCreatedResult ? Created(string.Empty, result) : Ok(result);
    }
}
