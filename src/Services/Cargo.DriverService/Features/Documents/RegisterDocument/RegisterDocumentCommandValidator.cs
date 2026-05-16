using FluentValidation;

namespace Cargo.DriverService.Features.Documents.RegisterDocument;

public class RegisterDocumentCommandValidator : AbstractValidator<RegisterDocumentCommand>
{
    public RegisterDocumentCommandValidator()
    {
        RuleFor(x => x.ObjectKey)
            .NotEmpty()
            .Must(key => key.StartsWith("drivers/"))
            .WithMessage("ObjectKey must begin with the 'drivers/' directory.");

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(BeAValidMimeType)
            .WithMessage("Invalid content type. Only JPEG, PNG, WEBP, and PDF are allowed.");

        // Here is where we strictly enforce the 15MB limit for the PUT-based upload workflow!
        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(15_728_640) // 15 MB in bytes
            .WithMessage("File size must be smaller than or equal to 15 MB.");

        RuleFor(x => x.OriginalFileName)
            .NotEmpty();
    }

    private bool BeAValidMimeType(string contentType)
    {
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "application/pdf" };
        return allowedTypes.Contains(contentType.ToLowerInvariant());
    }
}
