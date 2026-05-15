using Cargo.BuildingBlocks.CQRS;

namespace Cargo.CustomerService.Features.Auth.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber
) : ICommand<RegisterResponse>;