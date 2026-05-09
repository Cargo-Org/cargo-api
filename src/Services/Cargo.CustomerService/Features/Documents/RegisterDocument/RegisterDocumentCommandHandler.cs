using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Data;
using Cargo.CustomerService.Domain.Entities;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Documents.RegisterDocument;

// Notice we use your custom ICommandHandler<TCommand, TResponse>
public class RegisterDocumentCommandHandler(CustomerDbContext context) : ICommandHandler<RegisterDocumentCommand, Guid>
{
    private readonly CustomerDbContext _context = context;

    public async Task<ErrorOr<Guid>> Handle(RegisterDocumentCommand request, CancellationToken cancellationToken)
    {
        // 1. Eager load CustomerProfile along with its Documents collection
        var profile = await _context.CustomerProfiles
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.KeycloakUserId == request.KeycloakUserId, cancellationToken);

        if (profile == null)
        {
            return Error.NotFound(
                code: "Customer.NotFound",
                description: "Customer profile not found.");
        }

        // 2. Security Check: Prevent Path Traversal / Object Key Spoofing
        // We ensure the object key actually belongs to the user making the request.
        var expectedPrefix = $"customers/{profile.Id}/";
        if (!request.ObjectKey.StartsWith(expectedPrefix))
        {
            return Error.Forbidden(
                code: "Document.Forbidden",
                description: "You do not have permission to register a document under this storage path.");
        }

        // 3. Create the Domain Entity
        var document = CustomerDocument.Create(
            profile.Id,
            request.DocumentType,
            request.OriginalFileName,
            request.ObjectKey,
            request.ContentType,
            request.FileSizeBytes);

        // 4. Add to the Aggregate Root
        _context.CustomerDocuments.Add(document);

        // 5. Trigger Domain Logic (Updates Profile status based on documents)
        profile.RecomputeOnboardingStatus();

        // 6. Persist to Database
        await _context.SaveChangesAsync(cancellationToken);

        // Implicitly converts to ErrorOr<Guid> success state
        return document.Id;
    }
}