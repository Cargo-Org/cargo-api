using Cargo.BuildingBlocks.CQRS;

namespace Cargo.CustomerService.Features.Auth.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FullName,
    string PhoneNumber
) : ICommand<RegisterResponse>;