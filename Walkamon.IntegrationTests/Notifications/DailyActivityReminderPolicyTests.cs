using BLL.Service;
using Xunit;

namespace Walkamon.IntegrationTests.Notifications;

public sealed class DailyActivityReminderPolicyTests
{
    private static readonly DateTimeOffset Vietnam1800 =
        new(2026, 8, 17, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void At1759Local_ReminderIsNotYetDue()
    {
        var result = Evaluate(
            Vietnam1800.AddMinutes(-1),
            currentSteps: 3000,
            dailyGoal: 7000);

        Assert.Equal(DailyActivityReminderDecisionCode.NotYetDue, result.Decision);
    }

    [Fact]
    public void At1800Local_BelowGoalIsEligibleWithCorrectRemainingSteps()
    {
        var result = Evaluate(Vietnam1800, currentSteps: 3000, dailyGoal: 7000);

        Assert.Equal(DailyActivityReminderDecisionCode.Eligible, result.Decision);
        Assert.Equal(4000, result.RemainingSteps);
        Assert.Equal(new DateOnly(2026, 8, 17), result.LocalDate);
    }

    [Theory]
    [InlineData(6999, 7000, DailyActivityReminderDecisionCode.Eligible, 1)]
    [InlineData(7000, 7000, DailyActivityReminderDecisionCode.GoalReached, 0)]
    [InlineData(10000, 7000, DailyActivityReminderDecisionCode.GoalReached, 0)]
    [InlineData(7500, 10000, DailyActivityReminderDecisionCode.Eligible, 2500)]
    [InlineData(6000, 5000, DailyActivityReminderDecisionCode.GoalReached, 0)]
    public void GoalBoundaryAndCustomGoalAreApplied(
        int currentSteps,
        int dailyGoal,
        DailyActivityReminderDecisionCode expected,
        int expectedRemaining)
    {
        var result = Evaluate(Vietnam1800, currentSteps, dailyGoal);

        Assert.Equal(expected, result.Decision);
        Assert.Equal(expectedRemaining, result.RemainingSteps);
    }

    [Fact]
    public void NotificationDisabledSkipsDelivery()
    {
        var result = Evaluate(
            Vietnam1800,
            currentSteps: 3000,
            dailyGoal: 7000,
            notificationsEnabled: false);

        Assert.Equal(
            DailyActivityReminderDecisionCode.NotificationDisabled,
            result.Decision);
    }

    [Fact]
    public void MissingDeviceTokenSkipsDelivery()
    {
        var result = Evaluate(
            Vietnam1800,
            currentSteps: 3000,
            dailyGoal: 7000,
            hasActiveDeviceToken: false);

        Assert.Equal(
            DailyActivityReminderDecisionCode.MissingDeviceToken,
            result.Decision);
    }

    [Fact]
    public void SameUtcInstantIsEligibleOnlyForTimeZoneInsideLocalWindow()
    {
        var vietnam = Evaluate(
            Vietnam1800,
            currentSteps: 3000,
            dailyGoal: 7000,
            timeZoneId: "Asia/Ho_Chi_Minh");
        var utc = Evaluate(
            Vietnam1800,
            currentSteps: 3000,
            dailyGoal: 7000,
            timeZoneId: "UTC");

        Assert.Equal(DailyActivityReminderDecisionCode.Eligible, vietnam.Decision);
        Assert.Equal(DailyActivityReminderDecisionCode.NotYetDue, utc.Decision);
    }

    [Fact]
    public void WindowClosesAt2000AndDoesNotCatchUpAt2330()
    {
        var at2000 = Evaluate(
            Vietnam1800.AddHours(2),
            currentSteps: 3000,
            dailyGoal: 7000);
        var at2330 = Evaluate(
            Vietnam1800.AddHours(5).AddMinutes(30),
            currentSteps: 3000,
            dailyGoal: 7000);

        Assert.Equal(DailyActivityReminderDecisionCode.WindowClosed, at2000.Decision);
        Assert.Equal(DailyActivityReminderDecisionCode.WindowClosed, at2330.Decision);
    }

    [Fact]
    public void InvalidTimeZoneFallsBackWithoutChangingProductDecision()
    {
        var result = Evaluate(
            Vietnam1800,
            currentSteps: 3000,
            dailyGoal: 7000,
            timeZoneId: "Invalid/Walkamon-Time-Zone");

        Assert.Equal(DailyActivityReminderDecisionCode.Eligible, result.Decision);
        Assert.True(result.UsedFallbackTimeZone);
    }

    [Fact]
    public void LocalDateRolloverProducesIndependentEligibilityDate()
    {
        var dayOne = Evaluate(
            Vietnam1800,
            currentSteps: 3000,
            dailyGoal: 7000);
        var dayTwo = Evaluate(
            Vietnam1800.AddDays(1),
            currentSteps: 3000,
            dailyGoal: 7000);

        Assert.Equal(dayOne.LocalDate.AddDays(1), dayTwo.LocalDate);
        Assert.Equal(DailyActivityReminderDecisionCode.Eligible, dayTwo.Decision);
    }

    private static DailyActivityReminderPolicyResult Evaluate(
        DateTimeOffset utcNow,
        int currentSteps,
        int dailyGoal,
        bool notificationsEnabled = true,
        bool hasActiveDeviceToken = true,
        string timeZoneId = "Asia/Ho_Chi_Minh") =>
        DailyActivityReminderPolicy.Evaluate(new(
            utcNow,
            timeZoneId,
            AccountActive: true,
            notificationsEnabled,
            hasActiveDeviceToken,
            currentSteps,
            dailyGoal,
            new TimeOnly(18, 0),
            GraceMinutes: 120));
}
