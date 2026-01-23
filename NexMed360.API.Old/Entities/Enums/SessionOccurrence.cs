using System.Text.Json.Serialization;

namespace NexMed360.API.Entities.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionOccurrence
{
    Weekdays,
    Weekend,
    AllDays,
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
    Sunday,
}
