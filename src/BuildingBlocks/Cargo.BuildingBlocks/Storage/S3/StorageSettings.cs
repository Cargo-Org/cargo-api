namespace Cargo.BuildingBlocks.Storage.S3;

public class StorageSettings
{
    public const string SectionName = "Storage";

    public required string Endpoint { get; set; }
    public required string AccessKey { get; set; }
    public required string SecretKey { get; set; }
    public bool ForcePathStyle { get; set; } = true;
    public required string BucketName { get; set; }
}
