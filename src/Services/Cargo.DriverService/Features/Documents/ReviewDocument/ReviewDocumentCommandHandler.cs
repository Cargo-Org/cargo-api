using Cargo.BuildingBlocks.CQRS;
using Cargo.DriverService.Data;
using Cargo.DriverService.Domain.Enums;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Documents.ReviewDocument;

public sealed class ReviewDocumentCommandHandler(DriverDbContext dbContext) : ICommandHandler<ReviewDocumentCommand>
{

    public async Task<ErrorOr<Unit>> Handle(ReviewDocumentCommand request, CancellationToken cancellationToken)
    {
        // 1. Eager load the document, its parent profile, AND the sibling documents collection
        var document = await dbContext.DriverDocuments
            .Include(d => d.Driver)
                .ThenInclude(p => p.Documents)
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken);

        if (document == null)
        {
            return Error.NotFound(
                code: "Document.NotFound",
                description: $"Document with ID {request.DocumentId} was not found.");
        }

        if (document.ReviewStatus != DocumentReviewStatus.Pending)
        {
            return Error.Conflict(
                code: "Document.AlreadyReviewed",
                description: "This document has already been reviewed.");
        }

        // 2. Apply the review updates
        if (request.Status == DocumentReviewStatus.Rejected && !string.IsNullOrWhiteSpace(request.ReviewNote))
        {
            document.Reject(request.ReviewerKeycloakId, request.ReviewNote);
        }
        else if (request.Status == DocumentReviewStatus.Approved)
        {
            document.Approve(request.ReviewerKeycloakId);
        }
        else
        {
            return Error.Validation(
                code: "Document.InvalidReview",
                description: "Invalid review status or missing review note for rejection.");
        }

        // 3. Recompute Profile Onboarding Status
        document.Driver.RecomputeOnboardingStatus();

        // 4. Save Changes
        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
