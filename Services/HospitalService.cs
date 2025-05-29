using FinalProjectApi.Data;
using FinalProjectApi.Dtos.Doctor;
using FinalProjectApi.Dtos.Hospital;
using FinalProjectApi.Dtos.HospitalAdmin;
using FinalProjectApi.Dtos.Receptionist;
using FinalProjectApi.Entities;
using FinalProjectApi.Interfaces.Services;

namespace FinalProjectApi.Services
{
    public class HospitalService : IHospitalService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IUserService _userService;

        public HospitalService(ApplicationDbContext dbContext, IUserService userService)
        {
            _dbContext = dbContext;
            _userService = userService;
        }

        public async Task<string> CreateHospital(CreateHospitalDto dto)
        {
            var hospital = new Hospital();
            hospital.Name = dto.Name;
            hospital.Street = dto.Street;
            hospital.City = dto.City;
            hospital.District = dto.District;
            hospital.Phone = dto.Phone;
            hospital.Grade = dto.Grade;

            var createdHospital = await _dbContext.Hospitals.AddAsync(hospital);
            await _dbContext.SaveChangesAsync();
            
            return createdHospital.Entity.Id.ToString();
        }

        public async Task<string> CreateHospitalAdmin(CreateHospitalAdminDtso dto)
        {
            var userId = await _userService.RegisterUser(dto.PersonalInformation);
            
            if(userId != null) {
                var hospitalAdmin = new HospitalAdmin();
                hospitalAdmin.HospitalId = Guid.Parse(dto.HospitalId);
                hospitalAdmin.UserId = userId;

                var createdAdmin = await _dbContext.HospitalAdmins.AddAsync(hospitalAdmin);
                await _dbContext.SaveChangesAsync();
                return createdAdmin.Entity.Id.ToString();
            }

            return null;
        }

        public async Task<string> CreateReceptionist(CreateReceptionistDto dto)
        {
            var userId = await _userService.RegisterUser(dto.PersonalInformation);

            if (userId != null)
            {
                var receptionist = new Receptionist();
                receptionist.HospitalId = Guid.Parse(dto.HospitalId);
                receptionist.UserId = userId;
                receptionist.Department = dto.Department;
                receptionist.Location = dto.Location;

                var createdReceptionist = await _dbContext.Receptionists.AddAsync(receptionist);
                await _dbContext.SaveChangesAsync();
                return createdReceptionist.Entity.Id.ToString();
            }

            return null;
        }

        public async Task<string> CreateDoctor(CreateDoctorDto dto)
        {
            var userId = await _userService.RegisterUser(dto.PersonalInformation);

            if (userId != null)
            {
                var doctor = new Doctor();
                doctor.HospitalId = Guid.Parse(dto.HospitalId);
                doctor.UserId = userId;
                doctor.Speciality = dto.Speciality;

                var createdDoctor = await _dbContext.Doctors.AddAsync(doctor);
                await _dbContext.SaveChangesAsync();
                return createdDoctor.Entity.Id.ToString();
            }

            return null;
        }
    }
}
