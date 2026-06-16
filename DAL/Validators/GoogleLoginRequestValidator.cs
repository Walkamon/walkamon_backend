using DAL.DTO;
using FluentValidation;

namespace BLL.Validations;

public class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty()
            .WithMessage("Google idToken is required");
    }
}
