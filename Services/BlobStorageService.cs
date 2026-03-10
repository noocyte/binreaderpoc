using Azure;
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
        if (await _container.ExistsAsync())
        {
            await _container.DeleteAsync();
            Console.Write("Waiting for container deletion to propagate");
            while (true)
            {
                try
                {
                    await _container.CreateAsync();
                    Console.WriteLine(" done.");
                    return;
                }
                catch (RequestFailedException ex) when (ex.ErrorCode == "ContainerBeingDeleted")
                {
                    Console.Write(".");
                    await Task.Delay(2000);
                }
            }
        }

        await _container.CreateAsync();
    }

    private static string PackedBlobPath(string fieldName)
        => $"packed/{fieldName}";

    public async Task UploadPackedFieldBlobAsync(string fieldName, byte[] data)
    {
        var blobClient = _container.GetBlockBlobClient(PackedBlobPath(fieldName));
        using var stream = new MemoryStream(data);
        await blobClient.UploadAsync(stream, conditions: null);
    }

    public async Task<Optional<byte[]>> DownloadPackedFieldBlobAsync(string fieldName)
    {
        var path = PackedBlobPath(fieldName);

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
