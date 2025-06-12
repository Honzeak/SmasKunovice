

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmasKunovice.Avalonia.Models.JsonConverters;

public class OdidInt32Converter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number when reader.TryGetInt32(out var intValue):
                return intValue;
            case JsonTokenType.Number when reader.TryGetDouble(out var doubleValue):
                return (int)doubleValue;
            case JsonTokenType.String:
            {
                if (reader.GetString() is string stringValue)
                {
                    if (int.TryParse(stringValue, out var intValue))
                    {
                        return intValue;
                    }
                    if (double.TryParse(stringValue, out var doubleValue))
                    {
                        return (int)doubleValue;
                    }
                }

                break;
            }
        }

        throw new JsonException($"Unable to convert {reader.TokenType} to Int32.");
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}