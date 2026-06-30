using DAL.DTO;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Validators
{
    public class SetStepGoalRequestValidator : AbstractValidator<SetStepGoalRequest>
    {
        public SetStepGoalRequestValidator()
        {
            RuleFor(x => x.TargetSteps)
                .NotEmpty()
                .WithMessage("Target steps is required.")
                .GreaterThan(0)
                .WithMessage("Target steps must be greater than 0.")
                .LessThanOrEqualTo(100000)
                .WithMessage("Target steps cannot exceed 100000.");
        }
    }
}
