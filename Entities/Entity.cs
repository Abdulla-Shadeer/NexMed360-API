using System.ComponentModel.DataAnnotations;

namespace FinalProjectApi.Entities
{
    public class Entity
    {
        [Key]
        [Required]
        public Guid Id { get; set; }

        public DateTime? CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
