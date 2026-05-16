using FluentValidation;

namespace Cargo.DriverService.Features.Documents.GetUploadUrl;

public class GetUploadUrlQueryValidator : AbstractValidator<GetUploadUrlQuery>
{
    public GetUploadUrlQueryValidator()
    {
        RuleFor(x => x.DocumentType)
            .IsInEnum()
            .WithMessage("Invalid document type.");

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(BeAValidMimeType)
            .WithMessage("Invalid content type. Only JPEG, PNG, WEBP, and PDF are allowed.");
    }

    private bool BeAValidMimeType(string contentType)
    {
        var allowedTypes = new[]
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "application/pdf"
        };

        return allowedTypes.Contains(contentType.ToLowerInvariant());
    }
}
