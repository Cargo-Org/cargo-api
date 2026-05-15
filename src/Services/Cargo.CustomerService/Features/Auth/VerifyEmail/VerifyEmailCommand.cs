using Cargo.BuildingBlocks.CQRS;

namespace Cargo.CustomerService.Features.Auth.VerifyEmail;

public record VerifyEmailCommand(string Email, string OtpCode) : ICommand;