using System.Text.Json.Serialization;

namespace FinalProjectApi.Entities.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionStatus
{
    Started,
    Ended,
    Canceled,
}
