using Cargo.DriverService.Domain.Enums;
using FluentValidation;

namespace Cargo.DriverService.Features.Documents.ReviewDocument;

public class ReviewDocumentCommandValidator : AbstractValidator<ReviewDocumentCommand>
{
    public ReviewDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();

        RuleFor(x => x.Status)
            .IsInEnum()
            .NotEqual(DocumentReviewStatus.Pending)
            .WithMessage("Review status must be either Approved or Rejected.");

        // If Rejected, a ReviewNote is mandatory explaining why
        RuleFor(x => x.ReviewNote)
            .NotEmpty()
            .When(x => x.Status == DocumentReviewStatus.Rejected)
            .WithMessage("A review note is required when rejecting a document.");

        // If Approved, there should be no ReviewNote
        RuleFor(x => x.ReviewNote)
            .Empty()
            .When(x => x.Status == DocumentReviewStatus.Approved)
            .WithMessage("A review note should not be provided when approving a document.");
    }
}
