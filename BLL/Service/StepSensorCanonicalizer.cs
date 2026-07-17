using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DAL.DTO;

namespace BLL.Service;

public static class StepSensorCanonicalizer
{
    public static string ComputeHash(
        Guid sessionId,
        int sequence,
        string nonce,
        string sensorModeCode,
        IReadOnlyList<PvpStepEventRequest> events)
        => ComputeLegacyHash(sessionId, sequence, nonce, sensorModeCode, events);

    public static string ComputeHash(
        Guid sessionId,
        int sequence,
        string nonce,
        string sensorModeCode,
        int contractVersion,
        IReadOnlyList<PvpStepEventRequest> events,
        IReadOnlyList<StepMotionWindowRequest> motionWindows)
    {
        if (contractVersion < 2)
            return ComputeLegacyHash(sessionId, sequence, nonce, sensorModeCode, events);

        var builder = new StringBuilder();
        builder.Append("V2").Append('\n')
            .Append(sessionId.ToString("D")).Append('\n')
            .Append(sequence.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append(nonce).Append('\n')
            .Append(sensorModeCode);

        foreach (var item in events)
        {
            builder.Append('\n')
                .Append("E:")
                .Append(ToUnixMilliseconds(item.IntervalStartedAt)).Append(':')
                .Append(ToUnixMilliseconds(item.RecordedAt)).Append(':')
                .Append(item.StepCount.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.SensorStartTotal?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(':')
                .Append(item.SensorEndTotal?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        }

        foreach (var item in motionWindows)
        {
            builder.Append('\n')
                .Append("M:")
                .Append(ToUnixMilliseconds(item.WindowStartedAt)).Append(':')
                .Append(ToUnixMilliseconds(item.WindowEndedAt)).Append(':')
                .Append(item.SampleCount.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.AccelerometerSource).Append(':')
                .Append(item.GyroscopeAvailable ? "1" : "0").Append(':')
                .Append(item.ActivityAvailable ? "1" : "0").Append(':')
                .Append(item.AccelerationRmsMilli.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.AccelerationPeakMilli.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.JerkRmsMilli.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.GyroscopeRmsMilli?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(':')
                .Append(item.GyroscopePeakMilli?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(':')
                .Append(item.OrientationDeltaMilliDegrees?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(':')
                .Append(item.DominantFrequencyMilliHz.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.PeriodicityBps.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.GaitCycleCount.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.ActivityCode).Append(':')
                .Append(item.ActivityConfidence.ToString(CultureInfo.InvariantCulture));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static string ComputeLegacyHash(
        Guid sessionId,
        int sequence,
        string nonce,
        string sensorModeCode,
        IReadOnlyList<PvpStepEventRequest> events)
    {
        var builder = new StringBuilder();
        builder.Append(sessionId.ToString("D")).Append('\n')
            .Append(sequence.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append(nonce).Append('\n')
            .Append(sensorModeCode);

        foreach (var item in events)
        {
            builder.Append('\n')
                .Append(ToUnixMilliseconds(item.IntervalStartedAt)).Append(':')
                .Append(ToUnixMilliseconds(item.RecordedAt)).Append(':')
                .Append(item.StepCount.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.SensorStartTotal?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(':')
                .Append(item.SensorEndTotal?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static long ToUnixMilliseconds(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return new DateTimeOffset(utc).ToUnixTimeMilliseconds();
    }
}
