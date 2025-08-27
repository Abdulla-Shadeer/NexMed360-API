using System.Text.Json.Serialization;

namespace FinalProjectApi.Dtos.Search;

public enum EntityTypes
{
    [JsonPropertyName("patient")]
    Patient,
    [JsonPropertyName("doctor")]
    Doctor,
    [JsonPropertyName("hospital")]
    Hospital,
}
