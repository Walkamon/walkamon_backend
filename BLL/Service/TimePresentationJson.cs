using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BLL.Options;

namespace BLL.Service;

public static class TimePresentationJson
{
    public static JsonSerializerOptions CreateOptions(
        TimePresentationOptions options,
        JsonSerializerDefaults defaults = JsonSerializerDefaults.Web)
    {
        var serializerOptions = new JsonSerializerOptions(defaults);
        Configure(serializerOptions, options);
        return serializerOptions;
    }

    public static void Configure(
        JsonSerializerOptions serializerOptions,
        TimePresentationOptions options)
    {
        if (serializerOptions.Converters.Any(x => x is VietnamDateTimeJsonConverter))
            return;

        serializerOptions.Converters.Add(new VietnamDateTimeJsonConverter(options));
    }
}

public sealed class VietnamDateTimeJsonConverter : JsonConverter<DateTime>
{
    private readonly bool _useVietnamOffset;
    private readonly TimeSpan _offset;

    public VietnamDateTimeJsonConverter(TimePresentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _useVietnamOffset = options.UseVietnamOffset;
        _offset = TimeSpan.FromMinutes(options.OffsetMinutes);
    }

    public override DateTime Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
            throw new JsonException("A valid ISO-8601 timestamp is required.");

        if (TryParseToUtc(raw, _offset, out var parsed))
            return parsed;

        throw new JsonException("A valid ISO-8601 timestamp is required.");
    }

    public static bool TryParseToUtc(
        string raw,
        TimeSpan vietnamOffset,
        out DateTime value)
    {
        var hasExplicitOffset = raw.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
                                || HasTrailingOffset(raw);
        if (hasExplicitOffset &&
            DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                out var instant))
        {
            value = instant.UtcDateTime;
            return true;
        }

        if (!DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var localVietnamTime))
        {
            value = default;
            return false;
        }

        var unspecified = DateTime.SpecifyKind(localVietnamTime, DateTimeKind.Unspecified);
        value = new DateTimeOffset(unspecified, vietnamOffset).UtcDateTime;
        return true;
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateTime value,
        JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        if (_useVietnamOffset)
        {
            writer.WriteStringValue(new DateTimeOffset(utc).ToOffset(_offset));
            return;
        }

        writer.WriteStringValue(utc);
    }

    private static bool HasTrailingOffset(string value)
    {
        if (value.Length < 6) return false;
        var offsetStart = value.Length - 6;
        return (value[offsetStart] is '+' or '-')
               && value[offsetStart + 3] == ':'
               && char.IsDigit(value[offsetStart + 1])
               && char.IsDigit(value[offsetStart + 2])
               && char.IsDigit(value[offsetStart + 4])
               && char.IsDigit(value[offsetStart + 5]);
    }
}

public sealed class TimePresentationSerializer
{
    public TimePresentationSerializer(
        Microsoft.Extensions.Options.IOptions<TimePresentationOptions> options)
    {
        Options = TimePresentationJson.CreateOptions(options.Value);
    }

    public JsonSerializerOptions Options { get; }

    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
