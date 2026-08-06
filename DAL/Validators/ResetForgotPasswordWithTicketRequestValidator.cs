using DAL.DTO;
using FluentValidation;

namespace BLL.Validations;

public class ResetForgotPasswordWithTicketRequestValidator
    : AbstractValidator<ResetForgotPasswordWithTicketRequest>
{
    public ResetForgotPasswordWithTicketRequestValidator()
    {
        RuleFor(request => request.ResetToken)
            .NotEmpty()
            .WithMessage("Reset token is required");

        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .MinimumLength(6)
            .WithMessage("New password must be at least 6 characters")
            .Matches("[A-Z]")
            .WithMessage("New password must contain at least one uppercase letter")
            .Matches("[a-z]")
            .WithMessage("New password must contain at least one lowercase letter")
            .Matches("\\d")
            .WithMessage("New password must contain at least one number")
            .Matches("[\\W_]")
            .WithMessage("New password must contain at least one special character");
    }
}
