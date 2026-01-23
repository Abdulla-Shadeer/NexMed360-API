using System.Text.Json.Serialization;

namespace NexMed360Api.Dtos.Search;

public enum InputTypes
{
    [JsonPropertyName("name")]
    Name,
    [JsonPropertyName("email")]
    Email,
}
