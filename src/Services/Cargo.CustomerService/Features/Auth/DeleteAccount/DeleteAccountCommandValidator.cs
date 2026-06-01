using FluentValidation;

namespace Cargo.CustomerService.Features.Auth.DeleteAccount;

public sealed class DeleteAccountCommandValidator
    : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountCommandValidator()
    {
        RuleFor(x => x.KeycloakUserId)
            .NotEmpty().WithMessage("KeycloakUserId is required.");
    }
}
