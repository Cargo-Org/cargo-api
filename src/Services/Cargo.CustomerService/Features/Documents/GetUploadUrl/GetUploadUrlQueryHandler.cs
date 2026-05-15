using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Storage.S3;
using Cargo.CustomerService.Data;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Documents.GetUploadUrl
{
    public class GetUploadUrlQueryHandler(CustomerDbContext context, IStorageService storageService) : IQueryHandler<GetUploadUrlQuery, GetUploadUrlResponse>
    {
        private readonly CustomerDbContext _context = context;
        private readonly IStorageService _storageService = storageService;

        public async Task<ErrorOr<GetUploadUrlResponse>> Handle(GetUploadUrlQuery request, CancellationToken cancellationToken)
        {
            // 1. Look up the Customer Profile
            var profile = await _context.CustomerProfiles
                .FirstOrDefaultAsync(p => p.KeycloakUserId == request.KeycloakUserId, cancellationToken);

            if (profile == null)
            {
                return Error.NotFound(
                    code: "Customer.NotFound",
                    description: $"Customer profile with Keycloak ID {request.KeycloakUserId} not found.");
            }

            // 2. Map MIME type to file extension securely
            var ext = request.ContentType.ToLowerInvariant() switch
            {
                "image/jpeg" => "jpg",
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
            var objectKey = $"customers/{profile.Id}/{request.DocumentType}-{Guid.NewGuid()}.{ext}";

            // 4. Request the Pre-Signed URL from Storage Service
            var uploadUrl = await _storageService.GenerateUploadUrlAsync(objectKey, request.ContentType, cancellationToken);

            // Implicitly converts to ErrorOr<GetUploadUrlResponse> success state
            return new GetUploadUrlResponse(uploadUrl, objectKey);
        }
    }
}