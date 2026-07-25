using BLL.Service;
using DAL.DTO;
using DAL.Validators;
using Xunit;

namespace Walkamon.TransactionTests;

public class IntegrationConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("string")]
    [InlineData("ftp://example.com/image.png")]
    public void FirebaseImageUrl_InvalidValue_IsOmitted(string? imageUrl)
    {
        Assert.Null(FcmPushService.NormalizeImageUrl(imageUrl));
    }

    [Theory]
    [InlineData("https://res.cloudinary.com/demo/image/upload/sample.jpg")]
    [InlineData("http://example.com/image.png")]
    public void FirebaseImageUrl_AbsoluteHttpValue_IsPreserved(string imageUrl)
    {
        Assert.Equal(imageUrl, FcmPushService.NormalizeImageUrl(imageUrl));
    }

    [Fact]
    public void NotificationValidator_RejectsSwaggerPlaceholderImageUrl()
    {
        var validator = new CreateAdminNotificationRequestValidator();
        var result = validator.Validate(new CreateAdminNotificationRequest
        {
            TypeCode = "server_announcement",
            Title = "Test",
            Content = "Test",
            TargetAudienceCode = "all_users",
            ImageUrl = "string"
        });

        Assert.Contains(result.Errors, error => error.PropertyName == "ImageUrl");
    }

    [Theory]
    [InlineData("walkamonn@gmail.com")]
    [InlineData("\uFEFFwalkamonn@gmail.com")]
    [InlineData("  \uFEFFwalkamonn@gmail.com  ")]
    public void SmtpUsername_HiddenBom_IsRemoved(string username)
    {
        Assert.Equal(
            "walkamonn@gmail.com",
            GmailSmtpEmailSender.NormalizeUsername(username));
    }
}
