using DAL.DTO;
using FluentValidation;

namespace DAL.Validators;

public class UpdateLanguageModeRequestValidator
    : AbstractValidator<UpdateLanguageModeRequest>
{
    public UpdateLanguageModeRequestValidator()
    {
        RuleFor(x => x.LanguageCode)
            .NotEmpty()
            .Must(languageCode => new[] { "vi-VN", "en-US" }.Contains(languageCode))
            .WithMessage("Language code must be vi-VN or en-US");
    }
}
