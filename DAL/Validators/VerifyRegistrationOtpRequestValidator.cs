using DAL.DTO;
using FluentValidation;

namespace BLL.Validations;

public class VerifyRegistrationOtpRequestValidator
    : AbstractValidator<VerifyRegistrationOtpRequest>
{
    public VerifyRegistrationOtpRequestValidator()
    {
        RuleFor(request => request.RequestCode)
            .NotEmpty()
            .WithMessage("Request code is required");

        RuleFor(request => request.Otp)
            .Matches(@"^\d{6}$")
            .WithMessage("OTP must contain exactly 6 digits");
    }
}
