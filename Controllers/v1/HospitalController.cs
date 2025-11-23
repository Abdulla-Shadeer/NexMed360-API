using Asp.Versioning;
using NexMed360Api.Common;
using NexMed360Api.Common.Auth;
using NexMed360Api.Dtos.Doctor;
using NexMed360Api.Dtos.Hospital;
using NexMed360Api.Dtos.HospitalAdmin;
using NexMed360Api.Dtos.Receptionist;
using NexMed360Api.Dtos.User;
using NexMed360Api.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NexMed360Api.Controllers.v1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class HospitalController : ControllerBase
{

    private readonly IHospitalService _hospitalService;

    public HospitalController(IHospitalService hospitalService)
    {
        _hospitalService = hospitalService;
    }

    [HttpPost]
    [Route("Add")]
    [Authorize(Roles = UserRoles.SUPERUSER)]
    public async Task<ActionResult<Result<ViewHospitalDto>>> AddHospital(CreateHospitalDto dto)
    {
        return HandleCreationResult(await _hospitalService.Add(dto));
    }

    [HttpGet]
    [Route("GetAll")]
    [Authorize(Roles = $"{UserRoles.SUPERUSER}, {UserRoles.PATIENT}")]
    public async Task<ActionResult<Result<List<ViewHospitalDto>>>> GetAll()
    {

        var hospitalsResult = await _hospitalService.GetAll();
        if (hospitalsResult.IsSuccess)
        {
            return Ok(hospitalsResult);
        }

        return StatusCode(hospitalsResult.Error!.HttpStatus, hospitalsResult);
    }

    [HttpPost]
    [Route("AssignAdmin")]
    [Authorize(Roles = UserRoles.SUPERUSER)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddAdmin(CreateHospitalAdminDto dto)
    {
        return HandleCreationResult(await _hospitalService.AssignAdmin(dto));
    }

    [HttpPut]
    [Route("Update")]
    [Authorize(Roles = $"{UserRoles.SUPERUSER},{UserRoles.HOSPITAL_ADMIN}")]
    public async Task<ActionResult<Result<ViewHospitalDto>>> UpdateHospital(UpdateHospitalDto updateDto)
    {
        var updateResult = await _hospitalService.Update(updateDto);
        if (updateResult.IsSuccess)
        {
            return Ok(updateResult);
        }

        return StatusCode(updateResult.Error!.HttpStatus, updateResult);
    }

    [HttpGet]
    [Route("FindById/{id}")]
    [Authorize]
    public async Task<ActionResult<Result<ViewHospitalDto>>> FindById(string id)
    {
        var searchResult = await _hospitalService.FindById(id, User.IsInRole(UserRoles.SUPERUSER));
        if (searchResult.IsSuccess)
        {
            return Ok(searchResult);
        }

        return StatusCode(searchResult.Error!.HttpStatus, searchResult);
    }

    [HttpPost]
    [Route("AddReceptionist")]
    [Authorize(Roles = UserRoles.HOSPITAL_ADMIN)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddReceptionist(CreateReceptionistDto dto)
    {
        return HandleCreationResult(await _hospitalService.AddReceptionist(dto));
    }

    [HttpPost]
    [Route("AddDoctor")]
    [Authorize(Roles = UserRoles.HOSPITAL_ADMIN)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddDoctor(CreateDoctorDto dto)
    {
        return HandleCreationResult(await _hospitalService.AddDoctor(dto));
    }

    [HttpGet]
    [Route("GetMembers/{hospitalId}")]
    [Authorize(Roles = UserRoles.HOSPITAL_ADMIN)]
    public async Task<ActionResult<Result<List<ViewUserDto>>>> GetMembers(string hospitalId)
    {
        var getMembersResult = await _hospitalService.GetMembers(hospitalId, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (getMembersResult.IsSuccess)
        {
            return Ok(getMembersResult);
        }

        return StatusCode(getMembersResult.Error!.HttpStatus, getMembersResult);
    }

    private ActionResult<Result<T>> HandleCreationResult<T>(Result<T> result)
    {
        if (!result.IsSuccess)
            return StatusCode(result.Error!.HttpStatus, result);

        return Created(string.Empty, result);
    }
}
