using DAL.DTO;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Validators
{
    public class UpdatePetRequestValidator : AbstractValidator<UpdatePetRequest>
    {
        public UpdatePetRequestValidator()
        {
            RuleFor(x => x.PetName)
                .NotEmpty()
                .WithMessage("Pet name is required.")
                .MaximumLength(100)
                .WithMessage("Pet name cannot exceed 100 characters.");

            RuleFor(x => x.LifeForce)
                .GreaterThan(0)
                .WithMessage("Life Force must be greater than 0.");

            RuleFor(x => x.Energy)
                .GreaterThan(0)
                .WithMessage("Energy must be greater than 0.");

            RuleFor(x => x.Bond)
                .GreaterThan(0)
                .WithMessage("Bond must be greater than 0.");

            RuleFor(x => x.Exp)
                .GreaterThan(0)
                .WithMessage("EXP must be greater than 0.");

            RuleFor(x => x.LifeForceRate)
                .GreaterThan(0)
                .WithMessage("Life Force Rate must be greater than 0.")
                .LessThanOrEqualTo(10)
                .WithMessage("Life Force Rate cannot exceed 10.");

            RuleFor(x => x.EnergyRate)
                .GreaterThan(0)
                .WithMessage("Energy Rate must be greater than 0.")
                .LessThanOrEqualTo(10)
                .WithMessage("Energy Rate cannot exceed 10.");

            RuleFor(x => x.BondRate)
                .GreaterThan(0)
                .WithMessage("Bond Rate must be greater than 0.")
                .LessThanOrEqualTo(10)
                .WithMessage("Bond Rate cannot exceed 10.");

            RuleFor(x => x.ExpRate)
                .GreaterThan(0)
                .WithMessage("EXP Rate must be greater than 0.")
                .LessThanOrEqualTo(10)
                .WithMessage("EXP Rate cannot exceed 10.");
        }
    }
}
