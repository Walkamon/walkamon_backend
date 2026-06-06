using FluentValidation;

public class UpdateItemTypeValidator : AbstractValidator<UpdateItemTypeRequest>
{
    public UpdateItemTypeValidator()
    {
        RuleFor(x => x.ItemTypeName)
            .NotEmpty()
            .WithMessage("Item type name is required.")
            .MaximumLength(100)
            .WithMessage("Item type name must not exceed 100 characters.");
    }
}