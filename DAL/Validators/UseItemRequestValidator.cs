using DAL.DTO;
using FluentValidation;

namespace DAL.Validators;

public class UseItemRequestValidator : AbstractValidator<UseItemRequest>
{
    public UseItemRequestValidator()
    {
        RuleFor(x => x.ItemId)
            .NotEmpty()
            .WithMessage("ItemId is required.");
    }
}
