using DAL.DTO;
using FluentValidation;

namespace DAL.Validators;

public class CreateAdminAchievementRequestValidator
    : AbstractValidator<CreateAdminAchievementRequest>
{
    public CreateAdminAchievementRequestValidator()
    {
        Include(new AdminAchievementRequestRules<CreateAdminAchievementRequest>());
    }

    private sealed class AdminAchievementRequestRules<T> : AbstractValidator<T>
        where T : CreateAdminAchievementRequest
    {
        public AdminAchievementRequestRules()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required")
                .MaximumLength(100)
                .WithMessage("Title must not exceed 100 characters");

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage("Description must not exceed 500 characters");


            RuleFor(x => x.WalletAmount)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Wallet amount must be greater than or equal to 0");

            RuleFor(x => x)
                .Must(x => x.WalletAmount > 0 || x.RewardItems.Count > 0)
                .WithMessage("Achievement reward is required");

            RuleFor(x => x.MetricCode)
                .NotEmpty()
                .WithMessage("Metric code is required")
                .MaximumLength(30)
                .WithMessage("Metric code must not exceed 30 characters");

            RuleFor(x => x.TargetValue)
                .GreaterThan(0)
                .WithMessage("Target value must be greater than 0");

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



            RuleForEach(x => x.AssignmentConditions)
                .SetValidator(new AdminAchievementConditionRequestValidator());
        }
    }
}

public class UpdateAdminAchievementRequestValidator
    : AbstractValidator<UpdateAdminAchievementRequest>
{
    public UpdateAdminAchievementRequestValidator()
    {
        Include(new UpdateAdminAchievementRequestRules());
    }

    private sealed class UpdateAdminAchievementRequestRules
        : AbstractValidator<UpdateAdminAchievementRequest>
    {
        public UpdateAdminAchievementRequestRules()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required")
                .MaximumLength(100)
                .WithMessage("Title must not exceed 100 characters");

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage("Description must not exceed 500 characters");


            RuleFor(x => x.WalletAmount)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Wallet amount must be greater than or equal to 0");

            RuleFor(x => x)
                .Must(x => x.WalletAmount > 0 || x.RewardItems.Count > 0)
                .WithMessage("Achievement reward is required");

            RuleFor(x => x.MetricCode)
                .NotEmpty()
                .WithMessage("Metric code is required")
                .MaximumLength(30)
                .WithMessage("Metric code must not exceed 30 characters");

            RuleFor(x => x.TargetValue)
                .GreaterThan(0)
                .WithMessage("Target value must be greater than 0");

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



            RuleForEach(x => x.AssignmentConditions)
                .SetValidator(new AdminAchievementConditionRequestValidator());
        }
    }
}

public class AdminAchievementConditionRequestValidator
    : AbstractValidator<AdminAchievementConditionRequest>
{
    public AdminAchievementConditionRequestValidator()
    {
        RuleFor(x => x.ConditionCode)
            .NotEmpty()
            .WithMessage("Condition code is required")
            .MaximumLength(30)
            .WithMessage("Condition code must not exceed 30 characters");

        RuleFor(x => x.TargetValue)
            .GreaterThan(0)
            .WithMessage("Condition target value must be greater than 0");
    }
}
