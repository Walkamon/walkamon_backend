using FluentValidation;

public class UpdateItemValidator : AbstractValidator<UpdateItemRequest>
{
    public UpdateItemValidator()
    {
        RuleFor(x => x.ItemName)
            .NotEmpty()
            .WithMessage("Item name is required.")
            .MaximumLength(100);

        RuleFor(x => x.ItemTypeId)
            .NotEmpty()
            .WithMessage("Item type is required.");

        RuleFor(x => x.EffectTypeCode)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.EffectTypeCode));

        RuleFor(x => x.EffectValue)
            .GreaterThanOrEqualTo(0)
            .When(x => x.EffectValue.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}