using Cargo.BuildingBlocks.CQRS;
using MediatR;

namespace Cargo.DriverService.Features.Auth.ResetPassword;

public sealed record ResetPasswordCommand(
    string Email,
    string OtpCode,
    string NewPassword
) : ICommand<Unit>;
