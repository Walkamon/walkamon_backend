using DAL.DTO;
using FluentValidation;

namespace BLL.Validations;

public class ResendRegistrationOtpRequestValidator
    : AbstractValidator<ResendRegistrationOtpRequest>
{
    public ResendRegistrationOtpRequestValidator()
    {
        RuleFor(request => request.RequestCode)
            .NotEmpty()
            .WithMessage("Request code is required");
    }
}
