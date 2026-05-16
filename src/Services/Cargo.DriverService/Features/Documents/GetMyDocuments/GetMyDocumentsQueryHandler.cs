using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Storage.S3;
using Cargo.DriverService.Data;
using Cargo.DriverService.Domain.Enums;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Documents.GetMyDocuments;

public class GetMyDocumentsQueryHandler(DriverDbContext context, IStorageService storageService) : IQueryHandler<GetMyDocumentsQuery, List<DocumentResponse>>
{
    private readonly DriverDbContext _context = context;
    private readonly IStorageService _storageService = storageService;

    public async Task<ErrorOr<List<DocumentResponse>>> Handle(GetMyDocumentsQuery request, CancellationToken cancellationToken)
    {
        // 1. Verify the driver profile exists
        var profileExists = await _context.DriverProfiles
            .AnyAsync(p => p.KeycloakUserId == request.KeycloakUserId, cancellationToken);

        if (!profileExists)
        {
            return Error.NotFound(
                code: "Driver.NotFound",
                description: "Driver profile not found.");
        }

        // 2. Retrieve all documents for this user
        var documents = await _context.DriverDocuments
            .Where(d => d.Driver.KeycloakUserId == request.KeycloakUserId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        var responseList = new List<DocumentResponse>();

        // 3. Process each document and attach download URLs where appropriate
        foreach (var doc in documents)
        {
            string? downloadUrl = null;

            // Only generate S3/MinIO signed download URLs if the document is Approved
            if (doc.ReviewStatus == DocumentReviewStatus.Approved)
            {
                downloadUrl = await _storageService.GenerateDownloadUrlAsync(doc.ObjectKey, cancellationToken);
            }

            responseList.Add(new DocumentResponse(
                Id: doc.Id,
                DocumentType: doc.DocumentType,
                OriginalFileName: doc.OriginalFileName,
                ContentType: doc.ContentType,
                FileSizeBytes: doc.FileSizeBytes,
                ReviewStatus: doc.ReviewStatus,
                ReviewNote: doc.ReviewNote,
                UploadedAt: doc.UploadedAt,
                ReviewedAt: doc.ReviewedAt,
                DownloadUrl: downloadUrl
            ));
        }

        return responseList;
    }
}
