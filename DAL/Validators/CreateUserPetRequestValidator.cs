using DAL.DTO;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Validators
{
    public class CreateUserPetRequestValidator
        : AbstractValidator<CreateUserPetRequest>
    {
        public CreateUserPetRequestValidator()
        {
            RuleFor(x => x.PetName)
                .NotEmpty()
                .WithMessage("Pet name is required.")
                .MaximumLength(30)
                .WithMessage("Pet name cannot exceed 30 characters.")
                .Matches(@"^(?!\s+$).+")
                .WithMessage("Pet name cannot contain only whitespace.");
        }
    }
}
