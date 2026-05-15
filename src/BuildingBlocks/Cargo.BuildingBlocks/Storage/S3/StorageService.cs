using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Cargo.BuildingBlocks.Storage.S3;

public class StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public StorageService(IOptions<StorageSettings> options)
    {
        var settings = options.Value ?? throw new ArgumentNullException(nameof(options));

        var endpoint = string.IsNullOrWhiteSpace(settings.Endpoint) ? throw new ArgumentNullException("StorageSettings:Endpoint missing") : settings.Endpoint;
        var accessKey = string.IsNullOrWhiteSpace(settings.AccessKey) ? throw new ArgumentNullException("StorageSettings:AccessKey missing") : settings.AccessKey;
        var secretKey = string.IsNullOrWhiteSpace(settings.SecretKey) ? throw new ArgumentNullException("StorageSettings:SecretKey missing") : settings.SecretKey;
        _bucketName = string.IsNullOrWhiteSpace(settings.BucketName) ? throw new ArgumentNullException("StorageSettings:BucketName missing") : settings.BucketName;

        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = settings.ForcePathStyle
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