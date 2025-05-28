using FinalProjectApi.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Entities
{
    public class VisitHistory: Entity
    {
        public DateTime VisitDate { get; set; }
        public VisitReason Reason { get; set; }
        public Guid HospitalId { get; set; }
        public Hospital Hospital { get; set; }
        public string UserId { get; set; }
        public User User { get; set; }
    }
}
