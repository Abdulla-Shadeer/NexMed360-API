using NexMed360.Application.Access;
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
    private readonly WorkspaceAccess? _workspace;

    public PatientController(IPatientService patientService, WorkspaceAccess? workspace = null)
    {
        _patientService = patientService;
        _workspace = workspace;
    }

    [HttpPost]
    [Route("Create")]
    [Authorize]
    [Workspace(WorkspaceModes.Work, Permissions.PatientsRegister)]
    public async Task<ActionResult<Result<ViewPatientDto>>> Create(CreatePatientDto dto)
    {
        return HandleResult(await _patientService.CreatePatient(dto), true);
    }

    [HttpPost("EnableAccess")]
    [Authorize]
    public async Task<ActionResult<Result<ViewPatientDto>>> EnableAccess(EnablePatientAccessDto dto)
    {
        return HandleResult(await _patientService.EnablePatientAccess(dto, User.FindFirstValue(ClaimTypes.NameIdentifier)));
    }

    [HttpGet]
    [Route("Get/{patientId}")]
    [Authorize]
    [Workspace(WorkspaceModes.Work, Permissions.PatientsRead)]
    public async Task<ActionResult<Result<ViewPatientDto>>> GetPatient(string patientId)
    {
        return HandleResult(await _patientService.GetPatient(patientId));
    }

    [HttpPost]
    [Route("AddClinicalRecord")]
    [Authorize]
    [Workspace(WorkspaceModes.Work, Permissions.ClinicalRecordsWrite, true)]
    public async Task<ActionResult<Result<ViewClinicalRecordDto>>> AddClinicalRecord(AddClinicalRecordDto dto)
    {
        var practitionerRoleUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _patientService.AddToClinicalRecords(dto, practitionerRoleUserId!), true);
    }

    [HttpGet]
    [Route("GetClinicalRecord/{recordId}")]
    [Authorize]
    [ProducesResponseType(typeof(Result<ViewClinicalRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [Workspace(WorkspaceModes.Work + "," + WorkspaceModes.Patient, Permissions.ClinicalRecordsRead)]
    public async Task<ActionResult<Result<ViewClinicalRecordDto>>> GetClinicalRecord(string recordId)
    {
        var requesterUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _patientService.ViewClinicalRecord(
            recordId, requesterUserId, _workspace?.Mode == WorkspaceModes.Work));
    }

    [HttpGet]
    [Route("GetClinicalRecords/{patientId}")]
    [Authorize]
    [ProducesResponseType(typeof(Result<List<ViewClinicalRecordDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [Workspace(WorkspaceModes.Work + "," + WorkspaceModes.Patient, Permissions.ClinicalRecordsRead)]
    public async Task<ActionResult<Result<List<ViewClinicalRecordDto>>>> GetClinicalRecords(string patientId)
    {
        var requesterUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return HandleResult(await _patientService.ViewAllClinicalRecords(
            patientId, requesterUserId, _workspace?.Mode == WorkspaceModes.Work));
    }

    [HttpGet("MyClinicalRecords")]
    [Authorize]
    [Workspace(WorkspaceModes.Patient)]
    public async Task<ActionResult<Result<List<ViewClinicalRecordDto>>>> MyClinicalRecords()
    {
        return HandleResult(await _patientService.ViewAllClinicalRecords(
            _workspace!.PatientId!.Value.ToString(), User.FindFirstValue(ClaimTypes.NameIdentifier), false));
    }

    private ActionResult<Result<T>> HandleResult<T>(Result<T> result, bool isCreatedResult = false)
    {
        if (!result.IsSuccess)
            return StatusCode(result.Error!.HttpStatus, result);
        return isCreatedResult ? Created(string.Empty, result) : Ok(result);
    }
    [HttpGet("Me"), Authorize, Workspace(WorkspaceModes.Patient)]
    public async Task<ActionResult<Result<ViewPatientDto>>> Me() => HandleResult(await _patientService.GetOwnPatient());

}
