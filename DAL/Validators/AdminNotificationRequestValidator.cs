using DAL.DTO;
using FluentValidation;

namespace DAL.Validators;

public class CreateAdminNotificationRequestValidator
    : AbstractValidator<CreateAdminNotificationRequest>
{
    public CreateAdminNotificationRequestValidator()
    {
        RuleFor(x => x.TypeCode)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.TargetAudienceCode)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500)
            .When(x => x.Image == null);
    }
}

public class UpdateAdminNotificationRequestValidator
    : AbstractValidator<UpdateAdminNotificationRequest>
{
    public UpdateAdminNotificationRequestValidator()
    {
        RuleFor(x => x.TypeCode)
            .MaximumLength(30)
            .When(x => x.TypeCode != null);

        RuleFor(x => x.Title)
            .MaximumLength(120)
            .When(x => x.Title != null);

        RuleFor(x => x.Content)
            .MaximumLength(500)
            .When(x => x.Content != null);

        RuleFor(x => x.TargetAudienceCode)
            .MaximumLength(30)
            .When(x => x.TargetAudienceCode != null);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500)
            .When(x => x.Image == null);
    }
}
