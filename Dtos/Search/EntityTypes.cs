using System.Text.Json.Serialization;

namespace NexMed360Api.Dtos.Search;

public enum EntityTypes
{
    [JsonPropertyName("patient")]
    Patient,
    [JsonPropertyName("doctor")]
    Doctor,
    [JsonPropertyName("hospital")]
    Hospital,
}
