using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Data;
using Cargo.CustomerService.Domain.Enums;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Documents.ReviewDocument;

public sealed class ReviewDocumentCommandHandler(CustomerDbContext dbContext) : ICommandHandler<ReviewDocumentCommand>
{

    public async Task<ErrorOr<Unit>> Handle(ReviewDocumentCommand request, CancellationToken cancellationToken)
    {
        // 1. Eager load the document, its parent profile, AND the sibling documents collection
        var document = await dbContext.CustomerDocuments
            .Include(d => d.Customer)
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
        // Because we included p.Documents above, this domain method has all the data it needs
        document.Customer.RecomputeOnboardingStatus();

        // 4. Save Changes
        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value; // Implicitly converts to a successful ErrorOr<Unit>
    }
}