using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Entities
{
    public class Hospital: Entity
    {
        [Required]
        public string Name { get; set; }

        public string Street { get; set; }
        public string City { get; set; }
        public string District { get; set; }

        [Required]
        public string Phone { get; set; }
        public string Grade { get; set; }
        public List<HospitalAdmin> HospitalAdmins { get; set; } = new List<HospitalAdmin>();
        public List<Receptionist> Receptionists { get; set; } = new List<Receptionist>();
        public List<Doctor> Doctors { get; set; } = new List<Doctor>();
        public List<Appointment> Appointments { get; set; } = [];
    }
}
