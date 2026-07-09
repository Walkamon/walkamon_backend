using DAL.DTO;
using FluentValidation;

namespace DAL.Validators;

public class DeviceTokenRequestValidator : AbstractValidator<DeviceTokenRequest>
{
    public DeviceTokenRequestValidator()
    {
        RuleFor(x => x.FcmToken)
            .NotEmpty()
            .MaximumLength(512);
    }
}
