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
    [Route("CreateHospital")]
    [Authorize(Roles = UserRoles.SUPERUSER)]
    public async Task<ActionResult<Result<ViewHospitalDto>>> AddHospital(CreateHospitalDto dto) {
        try
        {
            return HandleCreationResult(await _hospitalService.CreateHospital(dto));
        }
        catch (Exception ex)
        {
            return HandleCreationException<ViewHospitalDto>("hospital", ex.Message);
        }
    }

    [HttpPost]
    [Route("CreateAdmin")]
    [Authorize(Roles = UserRoles.SUPERUSER)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddAdmin(CreateHospitalAdminDto dto)
    {
        try
        {
            return HandleCreationResult(await _hospitalService.CreateHospitalAdmin(dto));
        }
        catch (Exception ex)
        {
            return HandleCreationException<ViewUserDto>("hospital admin", ex.Message);
        }
    }

    [HttpPut]
    [Route("UpdateHospital")]
    [Authorize(Roles = $"{UserRoles.SUPERUSER},{UserRoles.HOSPITAL_ADMIN}")]
    public void UpdateHospital() { }

    [HttpPost]
    [Route("CreateReceptionist")]
    [Authorize(Roles = UserRoles.HOSPITAL_ADMIN)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddReceptionist(CreateReceptionistDto dto)
    {
        try
        {
            return HandleCreationResult(await _hospitalService.CreateReceptionist(dto));
        }
        catch (Exception ex)
        {
            return HandleCreationException<ViewUserDto>("receptionist", ex.Message);
        }
    }

    [HttpPost]
    [Route("CreateDoctor")]
    [Authorize(Roles = UserRoles.HOSPITAL_ADMIN)]
    public async Task<ActionResult<Result<ViewUserDto>>> AddDoctor(CreateDoctorDto dto)
    {
        try
        {
            return HandleCreationResult(await _hospitalService.CreateDoctor(dto));
        }
        catch (Exception e)
        {
            return HandleCreationException<ViewUserDto>("doctor", e.Message);
        }
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
