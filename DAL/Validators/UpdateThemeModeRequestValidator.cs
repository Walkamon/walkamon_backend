using DAL.DTO;
using FluentValidation;

namespace DAL.Validators;

public class UpdateThemeModeRequestValidator
    : AbstractValidator<UpdateThemeModeRequest>
{
    public UpdateThemeModeRequestValidator()
    {
        RuleFor(x => x.ThemeCode)
            .NotEmpty()
            .Must(themeCode => new[] { "light", "dark", "system" }.Contains(themeCode))
            .WithMessage("Theme code must be light, dark, or system");
    }
}
