using System.IO.Compression;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using DotNext;

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

    private static string PackedBlobPath(string fieldName)
        => $"packed/{fieldName}";

    public async Task UploadPackedFieldBlobAsync(string fieldName, byte[] data)
    {
        var blobClient = _container.GetBlockBlobClient(PackedBlobPath(fieldName));
        var compressed = Compress(data);
        using var stream = new MemoryStream(compressed);
        await blobClient.UploadAsync(stream, conditions: null);
    }

    public async Task<Optional<byte[]>> DownloadPackedFieldBlobAsync(string fieldName)
    {
        var blobClient = _container.GetBlockBlobClient(PackedBlobPath(fieldName));
        if (!await blobClient.ExistsAsync())
            return Optional<byte[]>.None;

        var response = await blobClient.DownloadContentAsync();
        return Decompress(response.Value.Content.ToArray());
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
            gzip.Write(data);
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
