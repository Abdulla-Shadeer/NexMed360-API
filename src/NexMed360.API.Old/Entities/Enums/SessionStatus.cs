using System.Text.Json.Serialization;

namespace NexMed360Api.Entities.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionStatus
{
    Started,
    Ended,
    Canceled,
}
