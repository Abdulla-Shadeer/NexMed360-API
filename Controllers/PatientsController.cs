using FinalProjectApi.Common.Auth;
using FinalProjectApi.Dtos.ClinicRecord;
using FinalProjectApi.Dtos.Patient;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

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
    public async Task<ActionResult<string>> AddPatient(CreatePatientDto dto)
    {
        var patientId = await _patientService.CreatePatient(dto);
        if (patientId != null)
        {
            return Ok(new { patientId });
        }
        return BadRequest("Patient can't be created!");
    }

    [HttpPost]
    [Route("AddVisitHistory")]
    [Authorize(Roles = UserRoles.RECEPTIONIST)]
    public async Task<ActionResult> AddVisitHistory(AddToVisitHistoryDto dto)
    {
        if (await _patientService.AddToVisitHistory(dto))
        {
            return Ok();
        }
        return BadRequest("Visit history can't be created!");
    }

    [HttpPost]
    [Route("AddClinicRecord")]
    [Authorize(Roles = UserRoles.DOCTOR)]
    public async Task<ActionResult> AddClinicRecord(AddClinicRecordDto dto)
    {
        var recordId = await _patientService.AddToClinicRecords(dto);
        if (!string.IsNullOrEmpty(recordId))
        {
            return Ok(new { recordId });
        }
        return BadRequest("Clinic record can't be created!");
    }

    [HttpGet]
    [Route("GetClinicRecords")]
    [Authorize(Roles = UserRoles.DOCTOR)]
    [ProducesResponseType(typeof(List<ClinicRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> GetClinicRecords([FromQuery] string patientId)
    {
        try
        {
            var records = await _patientService.GetAllClinicRecords(patientId);
            if (!records.IsNullOrEmpty())
            {
                return Ok(records);
            }
            return NotFound("No records found!");
        } catch(Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
