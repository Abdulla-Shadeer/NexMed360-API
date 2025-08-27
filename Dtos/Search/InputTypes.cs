using System.Text.Json.Serialization;

namespace FinalProjectApi.Dtos.Search;

public enum InputTypes
{
    [JsonPropertyName("name")]
    Name,
    [JsonPropertyName("email")]
    Email,
}
