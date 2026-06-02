using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Utils.OTP;
using Cargo.CustomerService.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Auth.VerifyPhone;

public sealed class VerifyPhoneCommandHandler(
    IOtpService otpService,
    CustomerDbContext dbContext)
    : ICommandHandler<VerifyPhoneCommand, Unit>
{
    public async Task<ErrorOr<Unit>> Handle(
        VerifyPhoneCommand command,
        CancellationToken cancellationToken)
    {
        // ── Step 1: Validate OtpCode ──────────────────────────────
        var isValid = await otpService.ValidateAsync(command.PhoneNumber, OtpPurpose.PhoneVerification, command.OtpCode, cancellationToken);

        if (!isValid)
        {
            return Error.Validation(
                code: "VerifyPhone.InvalidOtpCode",
                description: "Invalid or expired verification code.");
        }

        // ── Step 2: Find User Profile ─────────────────────────────
        var profile = await dbContext.CustomerProfiles
            .FirstOrDefaultAsync(p => p.PhoneNumber == command.PhoneNumber, cancellationToken);

        if (profile is null)
        {
            return Error.NotFound(
                code: "VerifyPhone.UserNotFound",
                description: "User profile not found for this phone number.");
        }

        // ── Step 3: Flip the PhoneVerified switch ─────────────────
        profile.SyncPhoneVerified(true);
        await dbContext.SaveChangesAsync(cancellationToken);

        // ── Step 4: Invalidate OtpCode ────────────────────────────
        await otpService.InvalidateAsync(command.PhoneNumber, OtpPurpose.PhoneVerification, cancellationToken);

        return Unit.Value;
    }
}
