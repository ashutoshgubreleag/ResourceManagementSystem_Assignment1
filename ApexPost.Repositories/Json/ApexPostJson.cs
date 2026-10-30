using System.Text.Json;
using System.Text.Json.Serialization;
using ApexPost.Models.ValueObjects;

namespace ApexPost.Repositories.Json;

/// <summary>
/// Serializer settings for the JSON store. Value objects are written as plain values
/// (<c>"UnitPrice": 4.95</c>, <c>"BranchCode": "BR-001"</c>) so the files stay readable and
/// map one-to-one onto database columns next week.
/// </summary>
public static class ApexPostJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new MoneyConverter());
        options.Converters.Add(new WeightConverter());
        options.Converters.Add(new DimensionsConverter());
        options.Converters.Add(new StringValueConverter<BranchCode>(v => new BranchCode(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<Sku>(v => new Sku(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<MaskedDocumentNumber>(MaskedDocumentNumber.FromMasked, v => v.Value));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    private sealed class MoneyConverter : JsonConverter<Money>
    {
        public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetDecimal());
        public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Amount);
    }

    private sealed class WeightConverter : JsonConverter<Weight>
    {
        public override Weight Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetDecimal());
        public override void Write(Utf8JsonWriter writer, Weight value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Kilograms);
    }

    private sealed class DimensionsConverter : JsonConverter<Dimensions>
    {
        public override Dimensions Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            decimal length = 0, width = 0, height = 0;
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected an object for Dimensions.");
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                var name = reader.GetString();
                reader.Read();
                switch (name?.ToUpperInvariant())
                {
                    case "LENGTHCM": length = reader.GetDecimal(); break;
                    case "WIDTHCM": width = reader.GetDecimal(); break;
                    case "HEIGHTCM": height = reader.GetDecimal(); break;
                    default: reader.Skip(); break;
                }
            }

            return length > 0 && width > 0 && height > 0 ? new Dimensions(length, width, height) : default;
        }

        public override void Write(Utf8JsonWriter writer, Dimensions value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("LengthCm", value.LengthCm);
            writer.WriteNumber("WidthCm", value.WidthCm);
            writer.WriteNumber("HeightCm", value.HeightCm);
            writer.WriteEndObject();
        }
    }

    private sealed class StringValueConverter<T>(Func<string, T> parse, Func<T, string> format) : JsonConverter<T> where T : struct
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetString() is { Length: > 0 } text ? parse(text) : default;

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteStringValue(format(value));
    }
}
