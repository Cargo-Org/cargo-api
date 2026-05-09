using Cargo.CustomerService.Domain.Enums;

namespace Cargo.CustomerService.Domain.Entities;

public sealed class CustomerProfile
{
    // Private constructor for EF Core. EF Core requires a parameterless
    // constructor to materialise entities from database rows.
    // Private prevents accidental use — always use the factory method.
    private CustomerProfile() { }

    public Guid Id { get; private set; }

    // The JWT sub claim. Immutable after creation.
    // This is the identity anchor — the permanent link to Keycloak.
    public string KeycloakUserId { get; private set; } = null!;

    // Nullable — Google login path creates profile before these are collected.
    public string? FullName { get; private set; }
    public string? PhoneNumber { get; private set; }

    // Denormalised from JWT at profile creation. Not used for auth.
    public string Email { get; private set; } = null!;

    // Starts false. Set to true when GET /me detects email_verified=true in JWT.
    public bool IsEmailVerified { get; private set; }

    // Never set by client. Always computed by RecomputeOnboardingStatus().
    public OnboardingStatus OnboardingStatus { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Navigation property — EF Core uses this to load related documents.
    // Private set prevents external code from replacing the collection.
    public IReadOnlyList<CustomerDocument> Documents { get; private set; }
        = new List<CustomerDocument>();

    public IReadOnlyList<CustomerAddress> Addresses { get; private set; }
        = new List<CustomerAddress>();

    // ── Factory method — email/password registration path ─────────────────
    // OnboardingStatus starts at MissingFiles because profile data is already
    // collected at registration (fullName and phoneNumber are required fields).
    public static CustomerProfile CreateForEmailRegistration(
        string keycloakUserId,
        string email,
        string fullName,
        string phoneNumber)
    {
        return new CustomerProfile
        {
            Id = Guid.NewGuid(),
            KeycloakUserId = keycloakUserId,
            Email = email,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            IsEmailVerified = false,
            OnboardingStatus = OnboardingStatus.MissingFiles,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    // ── Factory method — Google login / social path ────────────────────────
    // Name and phone are not available at profile auto-creation.
    // OnboardingStatus starts at MissingProfileData.
    public static CustomerProfile CreateForSocialLogin(
        string keycloakUserId,
        string email)
    {
        return new CustomerProfile
        {
            Id = Guid.NewGuid(),
            KeycloakUserId = keycloakUserId,
            Email = email,
            IsEmailVerified = true, // Google-authenticated users are already verified
            OnboardingStatus = OnboardingStatus.MissingProfileData,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    // ── Domain methods ─────────────────────────────────────────────────────

    public void UpdateProfile(string fullName, string phoneNumber)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
        UpdatedAt = DateTimeOffset.UtcNow;
        RecomputeOnboardingStatus();
    }

    public void SyncEmailVerified(bool isVerified)
    {
        if (IsEmailVerified == isVerified) return; // No-op if unchanged
        IsEmailVerified = isVerified;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // ── OnboardingStatus computation ───────────────────────────────────────
    // This method is the ONLY place OnboardingStatus is set.
    // Called after any change that could affect it.
    // Documents are passed in because the navigation property may not be
    // loaded in all contexts — explicit is safer than relying on lazy load.
    public void RecomputeOnboardingStatus(
        IReadOnlyList<CustomerDocument>? documents = null)
    {
        var docs = documents ?? Documents;

        if (string.IsNullOrWhiteSpace(FullName) ||
            string.IsNullOrWhiteSpace(PhoneNumber))
        {
            OnboardingStatus = OnboardingStatus.MissingProfileData;
            return;
        }

        // Any required document type missing entirely, or previously rejected
        var hasRequiredDocTypes = new[]
        {
            DocumentType.NationalIdFront,
            DocumentType.NationalIdBack,
            DocumentType.LiveFacePicture
        };

        bool allRequiredPresent = hasRequiredDocTypes.All(requiredType =>
            docs.Any(d =>
                d.DocumentType == requiredType &&
                d.ReviewStatus != DocumentReviewStatus.Rejected));

        if (!allRequiredPresent)
        {
            OnboardingStatus = OnboardingStatus.MissingFiles;
            return;
        }

        bool anyPending = docs.Any(d =>
            d.ReviewStatus == DocumentReviewStatus.Pending);

        if (anyPending)
        {
            OnboardingStatus = OnboardingStatus.PendingReview;
            return;
        }

        OnboardingStatus = OnboardingStatus.Verified;
    }
}