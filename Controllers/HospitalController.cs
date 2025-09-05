using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
using FinalProjectApi.Common.Auth;
using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.Hospital;
using FinalProjectApi.Dtos.HospitalAdmin;
using FinalProjectApi.Dtos.Receptionist;
using FinalProjectApi.Dtos.User;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinalProjectApi.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
[Authorize]
public class HospitalController : ControllerBase
{

    private readonly IHospitalService _hospitalService;

    public HospitalController(IHospitalService hospitalService) {
        _hospitalService = hospitalService;
    }

    [HttpPost]
    [Route("Add")]
    [Authorize(Roles = UserRoles.SUPERUSER)]
    public async Task<ActionResult<Result<ViewHospitalDto>>> AddHospital(CreateHospitalDto dto) {
        try
        {
            return HandleCreationResult(await _hospitalService.Add(dto));
        }
        catch (Exception ex)
        {
            return HandleCreationException<ViewHospitalDto>("hospital", ex.Message);
        }
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
        try
        {
            return HandleCreationResult(await _hospitalService.AssignAdmin(dto));
        }
        catch (Exception ex)
        {
            return HandleCreationException<ViewUserDto>("hospital admin", ex.Message);
        }
    }

    [HttpPut]
    [Route("Update")]
    [Authorize(Roles = $"{UserRoles.SUPERUSER},{UserRoles.HOSPITAL_ADMIN}")]
    public async Task<ActionResult<Result<ViewHospitalDto>>> UpdateHospital(UpdateHospitalDto updateDto) 
    {
        var updateResult = await _hospitalService.Update(updateDto);
        if (updateResult.IsSuccess) {
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
        try
        {
            return HandleCreationResult(await _hospitalService.AddReceptionist(dto));
        }
        catch (Exception ex)
        {
            return HandleCreationException<ViewUserDto>("receptionist", ex.Message);
        }
    }

    [HttpPost]
    [Route("AddDoctor")]
    [Authorize(Roles = UserRoles.HOSPITAL_ADMIN)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddDoctor(CreateDoctorDto dto)
    {
        try
        {
            return HandleCreationResult(await _hospitalService.AddDoctor(dto));
        }
        catch (Exception e)
        {
            return HandleCreationException<ViewUserDto>("doctor", e.Message);
        }
    }

    [HttpGet]
    [Route("GetMembers/{hospitalId}")]
    [Authorize(Roles = UserRoles.HOSPITAL_ADMIN)]
    public async Task<ActionResult<Result<List<ViewUserDto>>>> GetMembers(string hospitalId) 
    {
        var getMembersResult = await _hospitalService.GetMembers(hospitalId, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (getMembersResult.IsSuccess) {
            return Ok(getMembersResult);
        }

        return StatusCode(getMembersResult.Error!.HttpStatus, getMembersResult);
    }

    private ActionResult<Result<T>> HandleCreationResult<T>(Result<T> result) {
        if (!result.IsSuccess)
            return StatusCode(result.Error!.HttpStatus, result);

        return Created(string.Empty, result);
    }

    private ActionResult<Result<T>> HandleCreationException<T>(string createdEntity, string description)
    {
        return StatusCode(
            500,
            Result<T>.Failure(
                new ApiError(ErrorCode.Internal, $"An error occurred while creating the {createdEntity}.", 500, description)
                ));
    }
}
