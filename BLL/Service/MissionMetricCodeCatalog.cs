using System.Text.RegularExpressions;
using BLL.Exceptions;
using DAL.DTO;

namespace BLL.Service;

public static partial class MissionMetricCodeCatalog
{
    public const string Steps = "steps";
    public const string FeedPet = "feed_pet";
    public const string MissionCompleted = "mission_completed";
    public const string WalletEarned = "wallet_earned";
    public const string PetLevel = "pet_level";

    private static readonly HashSet<string> AllowedMetricCodes =
    [
        Steps,
        FeedPet,
        MissionCompleted,
        WalletEarned,
        PetLevel
    ];

    private static readonly IReadOnlyList<AdminMetricCodeResponse> MetricCodes =
    [
        new()
        {
            Code = Steps,
            Label = "BƯỚC CHÂN",
            ValueLabel = "Số bước"
        },
        new()
        {
            Code = FeedPet,
            Label = "CHO TINH LINH GIỌT SƯƠNG",
            ValueLabel = "Số lần"
        },
        new()
        {
            Code = MissionCompleted,
            Label = "NHIỆM VỤ ĐÃ HOÀN THÀNH",
            ValueLabel = "Số nhiệm vụ"
        },
        new()
        {
            Code = WalletEarned,
            Label = "GIỌT SƯƠNG TÍCH LŨY",
            ValueLabel = "Số Giọt Sương"
        },
        new()
        {
            Code = PetLevel,
            Label = "LEVEL TINH LINH",
            ValueLabel = "Level"
        }
    ];

    public static IReadOnlyList<AdminMetricCodeResponse> GetAll()
    {
        return MetricCodes;
    }

    public static string NormalizeOrThrow(string? metricCode)
    {
        var normalized = Normalize(metricCode);

        if (string.IsNullOrWhiteSpace(normalized)
            || !AllowedMetricCodes.Contains(normalized))
        {
            throw new BadRequestException("Unsupported metric code");
        }

        return normalized;
    }

    public static string GetTargetText(string metricCode, int targetValue)
    {
        var value = targetValue.ToString("N0");

        return metricCode switch
        {
            Steps => $"{value} bước",
            FeedPet => $"Cho Tinh Linh ăn {value} lần",
            MissionCompleted => $"Hoàn thành {value} nhiệm vụ",
            WalletEarned => $"{value} Giọt Sương",
            PetLevel => $"Level {value}",
            _ => $"{value} {metricCode}"
        };
    }

    private static string Normalize(string? metricCode)
    {
        if (string.IsNullOrWhiteSpace(metricCode))
        {
            return string.Empty;
        }

        var normalized = MetricCodeSeparatorRegex()
            .Replace(metricCode.Trim().ToLowerInvariant(), "_");

        return normalized switch
        {
            "level" => PetLevel,
            "petlevel" => PetLevel,
            "completed_mission" => MissionCompleted,
            "completed_missions" => MissionCompleted,
            "mission" => MissionCompleted,
            "missions" => MissionCompleted,
            "feedpet" => FeedPet,
            _ => normalized
        };
    }

    [GeneratedRegex(@"[\s\-]+")]
    private static partial Regex MetricCodeSeparatorRegex();
}
