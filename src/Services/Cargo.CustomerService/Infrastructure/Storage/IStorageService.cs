namespace Cargo.CustomerService.Infrastructure.Storage
{
    public interface IStorageService
    {
        Task<string> GenerateUploadUrlAsync(string objectKey, string contentType, CancellationToken ct);
        Task<string> GenerateDownloadUrlAsync(string objectKey, CancellationToken ct);
    }
}
