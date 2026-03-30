using FluentValidation;
using WalletApi.Application.DTOs.Wallet;

namespace WalletApi.Application.Validators;

public class TopUpRequestValidator : AbstractValidator<TopUpRequest>
{
    public TopUpRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Top-up amount must be greater than zero.")
            .LessThanOrEqualTo(1_000_000)
            .WithMessage("Top-up amount cannot exceed 1,000,000 in a single transaction.");

        RuleFor(x => x.Description)
            .MaximumLength(256)
            .WithMessage("Description cannot exceed 256 characters.")
            .When(x => x.Description is not null);
    }
}