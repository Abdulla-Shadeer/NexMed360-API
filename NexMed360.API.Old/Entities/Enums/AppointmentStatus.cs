using System.Text.Json.Serialization;

namespace NexMed360Api.Entities.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AppointmentStatus
{
    Pending,
    Confirmed,
    Arrived,
    InProgress,
    CancelRequested,
    Cancelled,
    Completed
}
