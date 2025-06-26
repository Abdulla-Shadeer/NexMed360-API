using FinalProjectApi.Common;
using FinalProjectApi.Common.ApplicationErrors;
using FinalProjectApi.Common.Auth;
using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.Hospital;
using FinalProjectApi.Dtos.HospitalAdmin;
using FinalProjectApi.Dtos.Receptionist;
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
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

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
    public async Task<ActionResult<Result<ViewHospitalAdminDto>>> AddAdmin(CreateHospitalAdminDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            return HandleCreationResult(await _hospitalService.CreateHospitalAdmin(dto));
        }
        catch (Exception ex)
        {
            return HandleCreationException<ViewHospitalAdminDto>("hospital admin", ex.Message);
        }
    }

    [HttpPut]
    [Route("UpdateHospital")]
    [Authorize(Roles = $"{UserRoles.SUPERUSER},{UserRoles.HOPITAL_ADMIN}")]
    public void UpdateHospital() { }

    [HttpPost]
    [Route("CreateReceptionist")]
    [Authorize(Roles = UserRoles.HOPITAL_ADMIN)]
    public async Task<ActionResult<Result<ViewReceptionistDto>>> AddReceptionist(CreateReceptionistDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            return HandleCreationResult(await _hospitalService.CreateReceptionist(dto));
        }
        catch (Exception ex)
        {
            return HandleCreationException<ViewReceptionistDto>("receptionist", ex.Message);
        }
    }

    [HttpPost]
    [Route("CreateDoctor")]
    [Authorize(Roles = UserRoles.HOPITAL_ADMIN)]
    public async Task<ActionResult<Result<ViewDoctorDto>>> AddDoctor(CreateDoctorDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            return HandleCreationResult(await _hospitalService.CreateDoctor(dto));
        }
        catch (Exception e)
        {
            return HandleCreationException<ViewDoctorDto>("doctor", e.Message);
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
