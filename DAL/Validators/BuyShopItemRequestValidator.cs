using DAL.DTO;
using FluentValidation;

namespace DAL.Validators;

public class BuyShopItemRequestValidator : AbstractValidator<BuyShopItemRequest>
{
    public BuyShopItemRequestValidator()
    {
        RuleFor(x => x.ShopItemId)
            .NotEmpty()
            .WithMessage("ShopItemId is required.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0.");
    }
}
