using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Dtos.Patient
{
    public class ViewPatientDto
    {
        public string Id { get; set; }
        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public DateOnly BirthDate { get; set; }

        public string Gender { get; set; }
    }
}
