using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AgenticBrowserAI.Helpers
{
    internal static class BasicHelper
    {
        public static T JsonToModel<T>(string json) where T : class, new()
        {
            if (String.IsNullOrWhiteSpace(json)) return null;

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                options.Converters.Add(new StringArrayJsonConverter());

                return JsonSerializer.Deserialize<T>(NormalizeJson(json), options) ?? new T();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Unable to parse planner JSON response.", ex);
            }
        }

        private static string NormalizeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            string normalized = json.Trim();

            normalized = Regex.Replace(
                normalized,
                @"\[(https?://[^\]]+)\]\((https?://[^\)]+)\)",
                match => match.Groups[2].Value);

            normalized = Regex.Replace(
                normalized,
                @"\[([A-Za-z_][\w-]*)=""([^""]+)""\]",
                "[$1='$2']");

            return normalized;
        }

        public static string ReadFile(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        public static void WriteFile(string path, string content)
        {
            File.WriteAllText(path, content);
        }

        private sealed class StringArrayJsonConverter : JsonConverter<string[]>
        {
            public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return Array.Empty<string>();
                }

                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    return new[] { ReadValueAsString(ref reader) };
                }

                var values = new List<string>();

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        return values.ToArray();
                    }

                    values.Add(ReadValueAsString(ref reader));
                }

                throw new JsonException("Expected end of string array.");
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

            private static string ReadValueAsString(ref Utf8JsonReader reader)
            {
                return reader.TokenType switch
                {
                    JsonTokenType.String => reader.GetString() ?? string.Empty,
                    JsonTokenType.Number => ReadNumberAsString(ref reader),
                    JsonTokenType.True => bool.TrueString,
                    JsonTokenType.False => bool.FalseString,
                    _ => throw new JsonException($"Unsupported function parameter token: {reader.TokenType}.")
                };
            }

            private static string ReadNumberAsString(ref Utf8JsonReader reader)
            {
                if (reader.TryGetInt64(out long integerValue))
                {
                    return integerValue.ToString(CultureInfo.InvariantCulture);
                }

                return reader.GetDouble().ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
