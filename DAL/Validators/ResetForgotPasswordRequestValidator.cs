using DAL.DTO;
using FluentValidation;

namespace BLL.Validations;

public class ResetForgotPasswordRequestValidator
    : AbstractValidator<ResetForgotPasswordRequest>
{
    public ResetForgotPasswordRequestValidator()
    {
        RuleFor(request => request.RequestCode)
            .NotEmpty()
            .WithMessage("Request code is required");

        RuleFor(request => request.Otp)
            .Matches(@"^\d{6}$")
            .WithMessage("OTP must contain exactly 6 digits");

        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .MinimumLength(6)
            .WithMessage("New password must be at least 6 characters")
            .Matches(@"[A-Z]")
            .WithMessage("New password must contain at least one uppercase letter")
            .Matches(@"[a-z]")
            .WithMessage("New password must contain at least one lowercase letter")
            .Matches(@"\d")
            .WithMessage("New password must contain at least one number")
            .Matches(@"[\W_]")
            .WithMessage("New password must contain at least one special character");
    }
}
