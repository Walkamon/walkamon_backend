namespace BLL.Service;

public static class NotificationTypeCatalog
{
    private static readonly IReadOnlyDictionary<string, string> TypeIcons =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["daily_reward"] = "gift",
            ["streak_reward"] = "flame",
            ["mission_complete"] = "target",
            ["achievement_complete"] = "trophy",
            ["challenge_invite"] = "swords",
            ["pvp_invite"] = "swords",
            ["friend_request"] = "user-plus",
            ["friend_accepted"] = "users",
            ["friend_removed"] = "user-minus",
            ["spirit_hungry"] = "utensils",
            ["spirit_ready_evolution"] = "sparkles",
            ["spirit_energy_full"] = "battery-full",
            ["spirit_bond_low"] = "heart-crack",
            ["spirit_level_up"] = "trending-up",
            ["item_purchased"] = "shopping-bag",
            ["item_sale"] = "badge-percent",
            ["limited_shop"] = "clock",
            ["pvp_result"] = "medal",
            ["maintenance"] = "wrench",
            ["patch_notes"] = "file-text",
            ["news"] = "newspaper",
            ["event"] = "calendar",
            ["compensation"] = "package-check",
            ["server_announcement"] = "megaphone"
        };

    public static bool IsValid(string typeCode)
    {
        return TypeIcons.ContainsKey(typeCode);
    }

    public static string GetIcon(string typeCode)
    {
        return TypeIcons.TryGetValue(typeCode, out var icon)
            ? icon
            : "bell";
    }
}
