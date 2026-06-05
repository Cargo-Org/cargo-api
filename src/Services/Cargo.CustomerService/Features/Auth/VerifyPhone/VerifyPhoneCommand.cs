using Cargo.BuildingBlocks.CQRS;

namespace Cargo.CustomerService.Features.Auth.VerifyPhone;

public record VerifyPhoneCommand(string PhoneNumber, string OtpCode) : ICommand;
