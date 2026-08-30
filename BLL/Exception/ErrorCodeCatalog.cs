namespace BLL.Exceptions;

internal static class ErrorCodeCatalog
{
    private static readonly IReadOnlyDictionary<string, string> Codes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["email already exists"] = "AUTH_EMAIL_ALREADY_EXISTS",
            ["username already exists"] = "AUTH_USERNAME_ALREADY_EXISTS",
            ["email or username already exists"] = "AUTH_IDENTITY_ALREADY_EXISTS",
            ["otp request is invalid"] = "AUTH_OTP_REQUEST_INVALID",
            ["otp has expired"] = "AUTH_OTP_EXPIRED",
            ["otp is invalid"] = "AUTH_OTP_INVALID",
            ["reset ticket is invalid or expired"] = "AUTH_RESET_TICKET_INVALID",
            ["account has been locked"] = "AUTH_ACCOUNT_LOCKED",
            ["account is not activated"] = "AUTH_ACCOUNT_NOT_ACTIVE",
            ["current password is invalid"] = "AUTH_CURRENT_PASSWORD_INVALID",
            ["invalid google token"] = "AUTH_GOOGLE_TOKEN_INVALID",
            ["google email is not verified"] = "AUTH_GOOGLE_EMAIL_UNVERIFIED",
            ["invalid google login"] = "AUTH_GOOGLE_LOGIN_INVALID",
            ["user not found"] = "USER_NOT_FOUND",
            ["user profile not found"] = "PROFILE_NOT_FOUND",

            ["starter pet not found"] = "PET_STARTER_NOT_FOUND",
            ["user already has a pet"] = "PET_ALREADY_EXISTS",
            ["pet not found"] = "PET_NOT_FOUND",
            ["friend spirit not found"] = "PET_FRIEND_NOT_FOUND",
            ["pet bond is already full"] = "PET_BOND_FULL",
            ["you have reached the maximum tap limit today"] = "PET_TAP_LIMIT_REACHED",
            ["pet life force is already full"] = "PET_LIFE_FORCE_FULL",
            ["you have reached the maximum feed limit today"] = "PET_FEED_LIMIT_REACHED",
            ["pet is already at the final evolution stage"] = "PET_FINAL_STAGE",
            ["evolution pet not found"] = "PET_EVOLUTION_NOT_FOUND",
            ["animation not found"] = "PET_ANIMATION_NOT_FOUND",

            ["leaderboard type is invalid"] = "STEPS_LEADERBOARD_TYPE_INVALID",
            ["daily login reward has already been claimed today"] = "DAILY_REWARD_ALREADY_CLAIMED",
            ["mission not found"] = "MISSION_NOT_FOUND",
            ["mission is not completed"] = "MISSION_NOT_COMPLETED",
            ["mission reward already claimed"] = "MISSION_REWARD_ALREADY_CLAIMED",
            ["mission is cancelled"] = "MISSION_CANCELLED",
            ["achievement not found"] = "ACHIEVEMENT_NOT_FOUND",
            ["achievement is not unlocked"] = "ACHIEVEMENT_NOT_UNLOCKED",
            ["achievement is not completed"] = "ACHIEVEMENT_NOT_COMPLETED",
            ["achievement reward already claimed"] = "ACHIEVEMENT_REWARD_ALREADY_CLAIMED",

            ["quantity must be greater than 0"] = "SHOP_QUANTITY_INVALID",
            ["total price amount is too large"] = "SHOP_TOTAL_TOO_LARGE",
            ["insufficient wallet balance"] = "WALLET_INSUFFICIENT_BALANCE",
            ["shop item not found"] = "SHOP_ITEM_NOT_FOUND",
            ["wallet not found"] = "WALLET_NOT_FOUND",
            ["item not found in inventory"] = "INVENTORY_ITEM_NOT_FOUND",
            ["inventory quantity is too large"] = "INVENTORY_QUANTITY_TOO_LARGE",
            ["pvp items can only be used through an active sprint match"] = "PVP_ITEM_REQUIRES_ACTIVE_MATCH",

            ["cannot send friend request to yourself"] = "FRIEND_REQUEST_SELF",
            ["friend request already exists"] = "FRIEND_REQUEST_ALREADY_SENT",
            ["already friends"] = "FRIEND_ALREADY_EXISTS",
            ["request not found"] = "FRIEND_REQUEST_NOT_FOUND",
            ["friendship not found"] = "FRIENDSHIP_NOT_FOUND",

            ["notification not found"] = "NOTIFICATION_NOT_FOUND",
            ["device token not found"] = "NOTIFICATION_DEVICE_TOKEN_NOT_FOUND",
            ["invalid notification type code"] = "NOTIFICATION_TYPE_INVALID",
        };

    public static string Resolve(string message, string fallback)
    {
        var normalized = message.Trim().TrimEnd('.');
        return Codes.TryGetValue(normalized, out var code) ? code : fallback;
    }
}
