using Asp.Versioning;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.ClinicalRecord;
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
    [Authorize(Roles = UserRoles.ORGANIZATION_MEMBER)]
    public async Task<ActionResult<Result<ViewPatientDto>>> Create(CreatePatientDto dto)
    {
        return HandleResult(await _patientService.CreatePatient(dto), true);
    }

    [HttpGet]
    [Route("Get/{patientId}")]
    [Authorize(Roles = $"{UserRoles.ORGANIZATION_MEMBER},{UserRoles.PRACTITIONER}")]
    public async Task<ActionResult<Result<ViewPatientDto>>> GetPatient(string patientId)
    {
        return HandleResult(await _patientService.GetPatient(patientId));
    }

    [HttpPost]
    [Route("AddClinicalRecord")]
    [Authorize(Roles = UserRoles.PRACTITIONER)]
    public async Task<ActionResult<Result<ViewClinicalRecordDto>>> AddClinicalRecord(AddClinicalRecordDto dto)
    {
        var practitionerRoleUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _patientService.AddToClinicalRecords(dto, practitionerRoleUserId!), true);
    }

    [HttpGet]
    [Route("GetClinicalRecord/{recordId}")]
    [Authorize(Roles = $"{UserRoles.PRACTITIONER}, {UserRoles.PATIENT}")]
    [ProducesResponseType(typeof(Result<ViewClinicalRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<ViewClinicalRecordDto>>> GetClinicalRecord(string recordId)
    {
        var requesterUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _patientService.ViewClinicalRecord(
            recordId, requesterUserId, User.IsInRole(UserRoles.PRACTITIONER)));
    }

    [HttpGet]
    [Route("GetClinicalRecords/{patientId}")]
    [Authorize(Roles = $"{UserRoles.PRACTITIONER}, {UserRoles.PATIENT}")]
    [ProducesResponseType(typeof(Result<List<ViewClinicalRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<List<ViewClinicalRecordDto>>>> GetClinicalRecords(string patientId)
    {
        var requesterUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _patientService.ViewAllClinicalRecords(
            patientId, requesterUserId, User.IsInRole(UserRoles.PRACTITIONER)));
    }

    private ActionResult<Result<T>> HandleResult<T>(Result<T> result, bool isCreatedResult = false)
    {
        if (!result.IsSuccess)
            return StatusCode(result.Error!.HttpStatus, result);
        return isCreatedResult ? Created(string.Empty, result) : Ok(result);
    }
}
