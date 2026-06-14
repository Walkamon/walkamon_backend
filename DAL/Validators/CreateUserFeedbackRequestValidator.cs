using DAL.DTO;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Validators
{
    public class CreateUserFeedbackRequestValidator
      : AbstractValidator<CreateUserFeedbackRequest>
    {
        public CreateUserFeedbackRequestValidator()
        {
            RuleFor(x => x.FeedbackTypeCode)
                .NotEmpty()
                .WithMessage("Feedback type is required.")
                .Must(x => x == "suggestion" || x == "bug_report")
                .WithMessage("Feedback type must be suggestion or bug_report.");

            RuleFor(x => x.Content)
                .NotEmpty()
                .WithMessage("Content is required.")
                .MaximumLength(2000)
                .WithMessage("Content cannot exceed 2000 characters.");
        }
    }
}