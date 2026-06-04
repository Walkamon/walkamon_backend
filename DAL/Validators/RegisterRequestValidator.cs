using DAL.DTO;
using FluentValidation;
using System.ComponentModel.DataAnnotations;

namespace BLL.Validations;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .MaximumLength(320)
            .WithMessage("Email must not exceed 320 characters")
            .Must(email => new EmailAddressAttribute().IsValid(email.Trim()))
            .WithMessage("Invalid email format");

        RuleFor(request => request.Username)
            .NotEmpty()
            .WithMessage("Username is required")
            .Must(username => username.Trim().Length is >= 3 and <= 30)
            .WithMessage("Username must be between 3 and 30 characters");

        RuleFor(request => request.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(6)
            .WithMessage("Password must be at least 6 characters")
            .Matches(@"[A-Z]")
            .WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]")
            .WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"\d")
            .WithMessage("Password must contain at least one number")
            .Matches(@"[\W_]")
            .WithMessage("Password must contain at least one special character");
    }
}
