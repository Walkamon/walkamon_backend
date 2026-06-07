using DAL.DTO;
using FluentValidation;
using System.ComponentModel.DataAnnotations;

namespace BLL.Validations;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .MaximumLength(320)
            .WithMessage("Email must not exceed 320 characters")
            .Must(email => new EmailAddressAttribute().IsValid(email.Trim()))
            .WithMessage("Invalid email format");
    }
}
