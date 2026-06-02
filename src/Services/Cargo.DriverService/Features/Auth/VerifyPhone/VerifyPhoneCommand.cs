using Cargo.BuildingBlocks.CQRS;

namespace Cargo.DriverService.Features.Auth.VerifyPhone;

public record VerifyPhoneCommand(string PhoneNumber, string OtpCode) : ICommand;
