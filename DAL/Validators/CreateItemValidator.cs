using FluentValidation;

public class CreateItemValidator : AbstractValidator<CreateItemRequest>
{
    public CreateItemValidator()
    {
        RuleFor(x => x.ItemName)
            .NotEmpty()
            .WithMessage("Item name is required.")
            .MaximumLength(100)
            .WithMessage("Item name must not exceed 100 characters.");

        RuleFor(x => x.ItemTypeId)
            .NotEmpty()
            .WithMessage("Item type is required.");

        RuleFor(x => x.EffectTypeCode)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.EffectTypeCode))
            .WithMessage("Effect type code must not exceed 50 characters.");

        RuleFor(x => x.EffectValue)
            .GreaterThanOrEqualTo(0)
            .When(x => x.EffectValue.HasValue)
            .WithMessage("Effect value must be greater than or equal to 0.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Description))
            .WithMessage("Description must not exceed 500 characters.");
    }
}