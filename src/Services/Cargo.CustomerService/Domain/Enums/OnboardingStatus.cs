namespace Cargo.CustomerService.Domain.Enums;

public enum OnboardingStatus
{
    // Profile auto-created via Google login but fullName and phoneNumber
    // not yet submitted. Google login path only.
    MissingProfileData = 0,

    // Profile data complete but required documents not yet uploaded,
    // or a previously submitted document was rejected by an admin.
    MissingFiles = 1,

    // All required documents uploaded and awaiting admin review.
    PendingReview = 2,

    // Admin has approved all required documents. Fully onboarded.
    Verified = 3
}