using DAL.DTO;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Validators
{
    public class AddPetExpRequestValidator
        : AbstractValidator<AddPetExpRequest>
    {
        public AddPetExpRequestValidator()
        {
            RuleFor(x => x.Exp)
                .GreaterThan(0)
                .WithMessage("Exp must be greater than 0.");
        }
    }
}
