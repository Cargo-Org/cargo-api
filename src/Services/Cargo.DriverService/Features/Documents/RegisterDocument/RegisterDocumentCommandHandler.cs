using Cargo.BuildingBlocks.CQRS;
using Cargo.DriverService.Data;
using Cargo.DriverService.Domain.Entities;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Documents.RegisterDocument;

public sealed class RegisterDocumentCommandHandler(DriverDbContext dbContext) : ICommandHandler<RegisterDocumentCommand, Guid>
{
    public async Task<ErrorOr<Guid>> Handle(RegisterDocumentCommand request, CancellationToken cancellationToken)
    {
        // 1. Eager load DriverProfile along with its Documents collection
        var profile = await dbContext.DriverProfiles
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.KeycloakUserId == request.KeycloakUserId, cancellationToken);

        if (profile == null)
        {
            return Error.NotFound(
                code: "Driver.NotFound",
                description: "Driver profile not found.");
        }

        // 2. Security Check: Prevent Path Traversal / Object Key Spoofing
        var expectedPrefix = $"drivers/{profile.Id}/";
        if (!request.ObjectKey.StartsWith(expectedPrefix))
        {
            return Error.Forbidden(
                code: "Document.Forbidden",
                description: "You do not have permission to register a document under this storage path.");
        }

        // 3. Create the Domain Entity
        var document = DriverDocument.Create(
            profile.Id,
            request.DocumentType,
            request.OriginalFileName,
            request.ObjectKey,
            request.ContentType,
            request.FileSizeBytes);

        // 4. Add to the Aggregate Root
        dbContext.DriverDocuments.Add(document);

        // 5. Trigger Domain Logic
        profile.RecomputeOnboardingStatus();

        // 6. Persist to Database
        await dbContext.SaveChangesAsync(cancellationToken);

        return document.Id;
    }
}
