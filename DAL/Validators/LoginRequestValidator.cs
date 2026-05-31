using DAL.DTO;
using FluentValidation;

namespace BLL.Validations
{
    public class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage("Email is required")
                .EmailAddress()
                .WithMessage("Invalid email format");

            RuleFor(x => x.Password)
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
}