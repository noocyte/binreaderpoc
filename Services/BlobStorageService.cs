using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using DotNext;
using ZstdSharp;

namespace BinReader.Services;

public class BlobStorageService
{
    private const string ContainerName = "temporal-fields";
    private readonly BlobContainerClient _container;

    public BlobStorageService(string connectionString)
    {
        var serviceClient = new BlobServiceClient(connectionString);
        _container = serviceClient.GetBlobContainerClient(ContainerName);
    }

    public async Task InitializeAsync()
    {
        await _container.CreateIfNotExistsAsync();

        await foreach (var blob in _container.GetBlobsAsync())
            await _container.DeleteBlobAsync(blob.Name);
    }

    private static string PackedBlobPath(string fieldName, char shard)
        => $"packed/{fieldName}/{shard}.bin";

    public async Task UploadPackedFieldBlobAsync(string fieldName, char shard, byte[] data)
    {
        var blobClient = _container.GetBlockBlobClient(PackedBlobPath(fieldName, shard));
        var compressed = Compress(data);
        using var stream = new MemoryStream(compressed);
        await blobClient.UploadAsync(stream, conditions: null);
    }

    public async Task<Optional<byte[]>> DownloadPackedFieldBlobAsync(string fieldName, char shard)
    {
        var blobClient = _container.GetBlockBlobClient(PackedBlobPath(fieldName, shard));
        if (!await blobClient.ExistsAsync())
            return Optional<byte[]>.None;

        var response = await blobClient.DownloadContentAsync();
        return Decompress(response.Value.Content.ToArray());
    }

    private static byte[] Compress(byte[] data)
    {
        using var compressor = new Compressor(7);
        return compressor.Wrap(data).ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        using var decompressor = new Decompressor();
        return decompressor.Unwrap(data).ToArray();
    }
}
