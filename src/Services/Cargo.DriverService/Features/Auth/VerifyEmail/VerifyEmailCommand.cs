using Cargo.BuildingBlocks.CQRS;

namespace Cargo.DriverService.Features.Auth.VerifyEmail;

public record VerifyEmailCommand(string Email, string OtpCode) : ICommand;
