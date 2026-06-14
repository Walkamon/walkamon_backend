using DAL.DTO;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Validators
{
    public class UpdateUserFeedbackRequestValidator
     : AbstractValidator<UpdateUserFeedbackRequest>
    {
        public UpdateUserFeedbackRequestValidator()
        {
            RuleFor(x => x.StatusCode)
                .NotEmpty()
                .WithMessage("Status is required.")
                .Must(x =>
                    x == "pending" ||
                    x == "in_progress" ||
                    x == "resolved" ||
                    x == "rejected")
                .WithMessage(
                    "Status must be pending, in_progress, resolved or rejected.");

            RuleFor(x => x.AdminNote)
                .MaximumLength(1000)
                .WithMessage("Admin note cannot exceed 1000 characters.");
        }
    }
}
