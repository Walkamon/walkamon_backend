namespace BLL.Service;

public static class ChallengeCycleDate
{
    private const int VietnamUtcOffsetHours = 7;

    public static DateOnly FromUtc(DateTime utcNow)
    {
        return DateOnly.FromDateTime(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)
                .AddHours(VietnamUtcOffsetHours));
    }
}
