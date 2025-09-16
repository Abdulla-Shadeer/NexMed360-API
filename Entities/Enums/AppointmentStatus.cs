using System.Text.Json.Serialization;

namespace FinalProjectApi.Entities.Enums;

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
