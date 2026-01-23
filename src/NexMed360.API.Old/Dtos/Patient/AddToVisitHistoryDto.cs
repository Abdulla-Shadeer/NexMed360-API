using NexMed360Api.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace NexMed360Api.Dtos.Patient
{
    public class AddToVisitHistoryDto
    {

        [Required]
        public VisitReason Reason { get; set; }

        [Required]
        public Guid HospitalId { get; set; }

        [Required]
        public string UserId { get; set; }
    }
}
