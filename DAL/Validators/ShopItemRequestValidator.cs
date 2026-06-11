using DAL.DTO;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Validators
{
    public class ShopItemRequestValidator : AbstractValidator<ShopItemRequest>
    {
        public ShopItemRequestValidator()
        {
            RuleFor(x => x.ItemId)
                .NotEmpty()
                .WithMessage("ItemId is required.");

            RuleFor(x => x.ItemQuantity)
                .GreaterThan(0)
                .WithMessage("Item quantity must be greater than 0.");

            RuleFor(x => x.PriceAmount)
                .GreaterThan(0)
                .WithMessage("Price amount must be greater than 0.");
        }
    }
}
