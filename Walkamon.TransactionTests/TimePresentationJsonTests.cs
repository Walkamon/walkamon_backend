using System.Text.Json;
using BLL.Options;
using BLL.Service;
using Xunit;

namespace Walkamon.TransactionTests;

[Trait("UC", "UC-72")]
public sealed class TimePresentationJsonTests
{
    [Fact]
    public void UtcTimestamp_IsWrittenWithVietnamOffset()
    {
        var options = TimePresentationJson.CreateOptions(new TimePresentationOptions());
        var json = JsonSerializer.Serialize(
            new TimestampEnvelope
            {
                Value = new DateTime(2026, 7, 28, 11, 30, 0, DateTimeKind.Utc)
            },
            options);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            "2026-07-28T18:30:00+07:00",
            document.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public void ExplicitUtcAndVietnamOffsets_ParseToSameInstant()
    {
        var options = TimePresentationJson.CreateOptions(new TimePresentationOptions());

        var utc = JsonSerializer.Deserialize<TimestampEnvelope>(
            """{"value":"2026-07-28T11:30:00Z"}""",
            options)!;
        var vietnam = JsonSerializer.Deserialize<TimestampEnvelope>(
            """{"value":"2026-07-28T18:30:00+07:00"}""",
            options)!;

        Assert.Equal(DateTimeKind.Utc, utc.Value.Kind);
        Assert.Equal(utc.Value, vietnam.Value);
    }

    [Fact]
    public void OffsetlessTimestamp_IsTreatedAsVietnamWallClock()
    {
        var options = TimePresentationJson.CreateOptions(new TimePresentationOptions());

        var result = JsonSerializer.Deserialize<TimestampEnvelope>(
            """{"value":"2026-07-28T18:30:00"}""",
            options)!;

        Assert.Equal(
            new DateTime(2026, 7, 28, 11, 30, 0, DateTimeKind.Utc),
            result.Value);
    }

    [Fact]
    public void FeatureFlagOff_WritesUtc()
    {
        var options = TimePresentationJson.CreateOptions(new TimePresentationOptions
        {
            UseVietnamOffset = false
        });
        var json = JsonSerializer.Serialize(
            new TimestampEnvelope
            {
                Value = new DateTime(2026, 7, 28, 11, 30, 0, DateTimeKind.Utc)
            },
            options);

        using var document = JsonDocument.Parse(json);
        Assert.EndsWith("Z", document.RootElement.GetProperty("value").GetString());
    }

    private sealed class TimestampEnvelope
    {
        public DateTime Value { get; set; }
    }
}
