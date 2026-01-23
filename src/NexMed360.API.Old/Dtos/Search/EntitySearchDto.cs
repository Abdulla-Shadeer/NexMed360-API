using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NexMed360Api.Dtos.Search;

public class EntitySearchDto
{
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EntityTypes Entity { get; set; }
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InputTypes InputType { get; set; }
    [Required]
    public string Input { get; set; }
}
