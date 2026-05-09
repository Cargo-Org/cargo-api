using Amazon.S3;
using Amazon.S3.Model;

namespace Cargo.CustomerService.Infrastructure.Storage;

public class StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public StorageService(IConfiguration configuration)
    {
        var endpoint = configuration["Storage:Endpoint"] ?? throw new ArgumentNullException("Storage:Endpoint missing");
        var accessKey = configuration["Storage:AccessKey"] ?? throw new ArgumentNullException("Storage:AccessKey missing");
        var secretKey = configuration["Storage:SecretKey"] ?? throw new ArgumentNullException("Storage:SecretKey missing");
        var forcePathStyle = configuration["Storage:ForcePathStyle"] ?? throw new ArgumentNullException("Storage:ForcePathStyle missing");
        _bucketName = configuration["Storage:BucketName"] ?? throw new ArgumentNullException("Storage:BucketName missing");

        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = bool.Parse(forcePathStyle)
        };

        _s3Client = new AmazonS3Client(accessKey, secretKey, config);
    }

    public Task<string> GenerateUploadUrlAsync(string objectKey, string contentType, CancellationToken ct = default)
    {
        /* 
         * NOTE: Size limits for PUT-based presigned URLs cannot be strictly enforced via S3 policy alone.
         * The full content-length-range enforcement requires a POST-based presigned URL (GeneratePresignedPost).
         * Because we are using HttpVerb.PUT here, size constraints must be strictly enforced via 
         * FluentValidation (e.g., <= 15MB) when the client subsequently registers the document in our API.
         */

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(15)
        };

        // Enforce the Content-Type header on the upload
        request.Headers.ContentType = contentType;

        // Note: GetPreSignedURL is a synchronous operation in the AWS SDK, 
        // but we wrap it in a Task to conform to the async interface contract.
        string url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public Task<string> GenerateDownloadUrlAsync(string objectKey, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(60)
        };

        string url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }
}