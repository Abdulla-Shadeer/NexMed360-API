using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace NexMed360Api.Common;

public class EnumMemberValueConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        foreach (var field in typeof(T).GetFields())
        {
            var attr = field.GetCustomAttribute<EnumMemberAttribute>();
            if (attr?.Value == value)
                return (T)field.GetValue(null)!;
        }

        throw new JsonException($"Unable to convert {value} to Enum {typeof(T)}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var field = typeof(T).GetField(value.ToString());
        var attr = field?.GetCustomAttribute<EnumMemberAttribute>();
        writer.WriteStringValue(attr?.Value ?? value.ToString());
    }
}
