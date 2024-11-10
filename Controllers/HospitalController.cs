using FinalProjectApi.Common.Auth;
using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.Hospital;
using FinalProjectApi.Dtos.HospitalAdmin;
using FinalProjectApi.Dtos.Receptionist;
using FinalProjectApi.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinalProjectApi.Controllers
{
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
        public async Task<ActionResult<string>> AddHospital(CreateHospitalDto dto) {
            var hospitalId = await _hospitalService.CreateHospital(dto);

            if (hospitalId != null)
            {
                return Ok(new { hospitalId });
            }
            return BadRequest("Hospital can't be created!");
        }

        [HttpPost]
        [Route("CreateAdmin")]
        [Authorize(Roles = UserRoles.SUPERUSER)]
        public async Task<ActionResult> AddAdmin(CreateHospitalAdminDtso dto) {
            var hospitalAdminId = await _hospitalService.CreateHospitalAdmin(dto);
            if(hospitalAdminId != null)
            {
                return Ok(new { hospitalAdminId });
            }
            return BadRequest("Hospital Admin can't be created!");
        }

        [HttpPut]
        [Route("UpdateHospital")]
        [Authorize(Roles = $"{UserRoles.SUPERUSER},{UserRoles.HOPITAL_ADMIN}")]
        public void UpdateHospital() { }

        [HttpPost]
        [Route("CreateReceptionist")]
        [Authorize(Roles = UserRoles.HOPITAL_ADMIN)]
        public async Task<ActionResult> AddReceptionist(CreateReceptionistDto dto)
        {
            var receptionistId = await _hospitalService.CreateReceptionist(dto);
            if (receptionistId != null)
            {
                return Ok(new { receptionistId });
            }
            return BadRequest("Receptionist can't be created!");
        }

        [HttpPost]
        [Route("CreateDoctor")]
        [Authorize(Roles = UserRoles.HOPITAL_ADMIN)]
        public async Task<ActionResult> AddDoctor(CreateDoctorDto dto)
        {
            var doctorId = await _hospitalService.CreateDoctor(dto);
            if (doctorId != null)
            {
                return Ok(new { doctorId });
            }
            return BadRequest("Doctor can't be created!");
        }
    }
}
