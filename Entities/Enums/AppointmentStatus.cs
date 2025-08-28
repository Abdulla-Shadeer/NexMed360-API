using System.Text.Json.Serialization;

namespace FinalProjectApi.Entities.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AppointmentStatus
{
    [JsonPropertyName("pending")]
    Pending,
    [JsonPropertyName("confirmed")]
    Confirmed,
    [JsonPropertyName("cancelRequested")]
    CancelRequested,
    [JsonPropertyName("cancelled")]
    Cancelled,
    [JsonPropertyName("completed")]
    Completed
}
