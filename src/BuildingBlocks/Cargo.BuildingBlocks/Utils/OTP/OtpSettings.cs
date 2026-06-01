namespace Cargo.BuildingBlocks.Utils.OTP;

/// <summary>Bound from appsettings.json → "Otp" section.</summary>
public sealed class OtpSettings
{
    public const string SectionName = "Otp";

    /// <summary>How long an OTP is valid (default 5 minutes).</summary>
    public int TtlMinutes { get; init; } = 5;

    /// <summary>Number of digits (default 5, max 8).</summary>
    public int CodeLength { get; init; } = 5;
}