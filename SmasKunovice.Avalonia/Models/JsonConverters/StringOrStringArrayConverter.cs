using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmasKunovice.Avalonia.Models.JsonConverters;

public class StringOrStringArrayConverter : JsonConverter<string[]>
{
    public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                // If it's a single string, return it as a single-element array
                return [reader.GetString()!];
            case JsonTokenType.StartArray:
            {
                // Handle regular array
                var results = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        results.Add(reader.GetString()!);
                    }
                }

                return results.ToArray();
            }
            default:
                throw new JsonException($"Cannot convert {reader.TokenType} to string array");
        }
    }

    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}