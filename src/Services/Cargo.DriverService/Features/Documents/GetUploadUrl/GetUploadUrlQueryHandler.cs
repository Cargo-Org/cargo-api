using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Storage.S3;
using Cargo.DriverService.Data;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Documents.GetUploadUrl;

public class GetUploadUrlQueryHandler(DriverDbContext context, IStorageService storageService) : IQueryHandler<GetUploadUrlQuery, GetUploadUrlResponse>
{
    private readonly DriverDbContext _context = context;
    private readonly IStorageService _storageService = storageService;

    public async Task<ErrorOr<GetUploadUrlResponse>> Handle(GetUploadUrlQuery request, CancellationToken cancellationToken)
    {
        // 1. Look up the Driver Profile
        var profile = await _context.DriverProfiles
            .FirstOrDefaultAsync(p => p.KeycloakUserId == request.KeycloakUserId, cancellationToken);

        if (profile == null)
        {
            return Error.NotFound(
                code: "Driver.NotFound",
                description: $"Driver profile with Keycloak ID {request.KeycloakUserId} not found.");
        }

        // 2. Map MIME type to file extension securely
        var ext = request.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/jpg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            "application/pdf" => "pdf",
            _ => null
        };

        if (ext == null)
        {
            return Error.Validation(
                code: "Document.InvalidContentType",
                description: "Unsupported content type.");
        }

        // 3. Generate the secure, server-side Object Key
        var objectKey = $"drivers/{profile.Id}/{request.DocumentType}-{Guid.NewGuid()}.{ext}";

        // 4. Request the Pre-Signed URL from Storage Service
        var uploadUrl = await _storageService.GenerateUploadUrlAsync(objectKey, request.ContentType, cancellationToken);

        return new GetUploadUrlResponse(uploadUrl, objectKey);
    }
}
