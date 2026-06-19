using DAL.DTO;
using FluentValidation;

namespace DAL.Validators;

public class CreateAdminChallengeRequestValidator
    : AbstractValidator<CreateAdminChallengeRequest>
{
    public CreateAdminChallengeRequestValidator()
    {
        Include(new AdminChallengeRequestRules<CreateAdminChallengeRequest>());
    }

    private sealed class AdminChallengeRequestRules<T> : AbstractValidator<T>
        where T : CreateAdminChallengeRequest
    {
        public AdminChallengeRequestRules()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required")
                .MaximumLength(100)
                .WithMessage("Title must not exceed 100 characters");

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage("Description must not exceed 500 characters")
                .When(x => !string.IsNullOrWhiteSpace(x.Description));

            RuleFor(x => x.MetricCode)
                .NotEmpty()
                .WithMessage("Metric code is required")
                .MaximumLength(30)
                .WithMessage("Metric code must not exceed 30 characters");

            RuleFor(x => x.ChallengeTypeCode)
                .NotEmpty()
                .WithMessage("Challenge type code is required")
                .MaximumLength(20)
                .WithMessage("Challenge type code must not exceed 20 characters");

            RuleFor(x => x.TargetValue)
                .GreaterThan(0)
                .WithMessage("Target value must be greater than 0");

            RuleFor(x => x.WalletAmount)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Wallet amount must be greater than or equal to 0");

            RuleFor(x => x)
                .Must(x => !x.StartAt.HasValue
                    || !x.EndAt.HasValue
                    || x.StartAt <= x.EndAt)
                .WithMessage("Start date must be before or equal to end date");

            RuleFor(x => x)
                .Must(x => x.WalletAmount > 0 || x.RewardItems.Count > 0)
                .WithMessage("Challenge reward is required");

            RuleForEach(x => x.RewardItems)
                .ChildRules(item =>
                {
                    item.RuleFor(x => x.ItemId)
                        .NotEmpty()
                        .WithMessage("Reward item id is required");

                    item.RuleFor(x => x.Quantity)
                        .GreaterThan(0)
                        .WithMessage("Reward item quantity must be greater than 0");
                });
        }
    }
}

public class UpdateAdminChallengeRequestValidator
    : AbstractValidator<UpdateAdminChallengeRequest>
{
    public UpdateAdminChallengeRequestValidator()
    {
        Include(new UpdateAdminChallengeRequestRules());
    }

    private sealed class UpdateAdminChallengeRequestRules
        : AbstractValidator<UpdateAdminChallengeRequest>
    {
        public UpdateAdminChallengeRequestRules()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required")
                .MaximumLength(100)
                .WithMessage("Title must not exceed 100 characters");

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage("Description must not exceed 500 characters")
                .When(x => !string.IsNullOrWhiteSpace(x.Description));

            RuleFor(x => x.MetricCode)
                .NotEmpty()
                .WithMessage("Metric code is required")
                .MaximumLength(30)
                .WithMessage("Metric code must not exceed 30 characters");

            RuleFor(x => x.ChallengeTypeCode)
                .NotEmpty()
                .WithMessage("Challenge type code is required")
                .MaximumLength(20)
                .WithMessage("Challenge type code must not exceed 20 characters");

            RuleFor(x => x.TargetValue)
                .GreaterThan(0)
                .WithMessage("Target value must be greater than 0");

            RuleFor(x => x.WalletAmount)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Wallet amount must be greater than or equal to 0");

            RuleFor(x => x)
                .Must(x => !x.StartAt.HasValue
                    || !x.EndAt.HasValue
                    || x.StartAt <= x.EndAt)
                .WithMessage("Start date must be before or equal to end date");

            RuleFor(x => x)
                .Must(x => x.WalletAmount > 0 || x.RewardItems.Count > 0)
                .WithMessage("Challenge reward is required");

            RuleForEach(x => x.RewardItems)
                .ChildRules(item =>
                {
                    item.RuleFor(x => x.ItemId)
                        .NotEmpty()
                        .WithMessage("Reward item id is required");

                    item.RuleFor(x => x.Quantity)
                        .GreaterThan(0)
                        .WithMessage("Reward item quantity must be greater than 0");
                });
        }
    }
}
