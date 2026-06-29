using DAL.DTO;
using FluentValidation;

namespace DAL.Validators;

public class CreateAdminMissionRequestValidator
    : AbstractValidator<CreateAdminMissionRequest>
{
    public CreateAdminMissionRequestValidator()
    {
        Include(new AdminMissionRequestRules<CreateAdminMissionRequest>());
    }

    private sealed class AdminMissionRequestRules<T> : AbstractValidator<T>
        where T : CreateAdminMissionRequest
    {
        public AdminMissionRequestRules()
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

            RuleFor(x => x.StartAt)
                .NotEmpty()
                .When(x => x.MissionTypeCode == "daily")
                .WithMessage("Start date is required for daily missions");

            RuleFor(x => x.EndAt)
                .NotEmpty()
                .When(x => x.MissionTypeCode == "daily")
                .WithMessage("End date is required for daily missions");

            RuleFor(x => x.EndAt)
                .GreaterThan(x => x.StartAt)
                .When(x => x.StartAt.HasValue && x.EndAt.HasValue)
                .WithMessage("End date must be greater than start date");

            RuleFor(x => x.WalletAmount)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Wallet amount must be greater than or equal to 0");

            RuleFor(x => x)
                .Must(x => x.WalletAmount > 0 || x.RewardItems.Count > 0)
                .WithMessage("Mission reward is required");

            RuleFor(x => x.CompletionConditions)
                .NotEmpty()
                .WithMessage("Completion condition is required");

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

            RuleForEach(x => x.CompletionConditions)
                .SetValidator(new AdminMissionConditionRequestValidator());

            RuleForEach(x => x.AssignmentConditions)
                .SetValidator(new AdminMissionConditionRequestValidator());
        }
    }
}

public class UpdateAdminMissionRequestValidator
    : AbstractValidator<UpdateAdminMissionRequest>
{
    public UpdateAdminMissionRequestValidator()
    {
        Include(new UpdateAdminMissionRequestRules());
    }

    private sealed class UpdateAdminMissionRequestRules
        : AbstractValidator<UpdateAdminMissionRequest>
    {
        public UpdateAdminMissionRequestRules()
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

            RuleFor(x => x.StartAt)
                .NotEmpty()
                .When(x => x.MissionTypeCode == "daily")
                .WithMessage("Start date is required for daily missions");

            RuleFor(x => x.EndAt)
                .NotEmpty()
                .When(x => x.MissionTypeCode == "daily")
                .WithMessage("End date is required for daily missions");

            RuleFor(x => x.EndAt)
                .GreaterThan(x => x.StartAt)
                .When(x => x.StartAt.HasValue && x.EndAt.HasValue)
                .WithMessage("End date must be greater than start date");

            RuleFor(x => x.WalletAmount)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Wallet amount must be greater than or equal to 0");

            RuleFor(x => x)
                .Must(x => x.WalletAmount > 0 || x.RewardItems.Count > 0)
                .WithMessage("Mission reward is required");

            RuleFor(x => x.CompletionConditions)
                .NotEmpty()
                .WithMessage("Completion condition is required");

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

            RuleForEach(x => x.CompletionConditions)
                .SetValidator(new AdminMissionConditionRequestValidator());

            RuleForEach(x => x.AssignmentConditions)
                .SetValidator(new AdminMissionConditionRequestValidator());
        }
    }
}

public class AdminMissionConditionRequestValidator
    : AbstractValidator<AdminMissionConditionRequest>
{
    public AdminMissionConditionRequestValidator()
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
