using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.Hospital
{
    public class CreateHospitalDto
    {
        [Required]
        public string Name { get; set; }

        public string Street { get; set; }
        public string City { get; set; }
        public string District { get; set; }

        [Required]
        public string Phone { get; set; }
        public string Grade { get; set; }
    }
}
