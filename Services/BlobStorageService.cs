using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using DotNext;
using Microsoft.Extensions.Caching.Memory;

namespace BinReader.Services;

public class BlobStorageService
{
    private const string ContainerName = "temporal-fields";
    private readonly BlobContainerClient _container;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public BlobStorageService(string connectionString)
    {
        var serviceClient = new BlobServiceClient(connectionString);
        _container = serviceClient.GetBlobContainerClient(ContainerName);
    }

    public async Task InitializeAsync()
    {
        await _container.CreateIfNotExistsAsync();
    }

    private static string BlobPath(Guid articleId, string fieldName)
        => $"{articleId}/{fieldName}";

    public async Task UploadFieldBlobAsync(Guid articleId, string fieldName, byte[] data)
    {
        var blobClient = _container.GetBlockBlobClient(BlobPath(articleId, fieldName));
        using var stream = new MemoryStream(data);
        await blobClient.UploadAsync(stream, conditions: null);
    }

    public async Task<Optional<byte[]>> DownloadFieldBlobAsync(Guid articleId, string fieldName)
    {
        var path = BlobPath(articleId, fieldName);

        if (_cache.TryGetValue<byte[]>(path, out var cached))
            return cached!;

        var blobClient = _container.GetBlockBlobClient(path);
        if (!await blobClient.ExistsAsync())
            return Optional<byte[]>.None;

        var response = await blobClient.DownloadContentAsync();
        var data = response.Value.Content.ToArray();

        _cache.Set(path, data);
        return data;
    }
}
