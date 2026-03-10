using BenchmarkDotNet.Attributes;
using BinReader.DataGeneration;
using BinReader.Models;
using BinReader.PackedBlobFormat;
using BinReader.Services;

namespace BinReader.Benchmarks;

[SimpleJob(warmupCount: 1, iterationCount: 3)]
[MemoryDiagnoser]
public class PackedWriteBenchmarks
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")!;

    private BlobStorageService _storage = null!;
    private List<(Guid ArticleId, IReadOnlyList<FieldChange> Changes)> _priceArticleData = null!;

    [GlobalSetup]
    public void Setup()
    {
        _storage = new BlobStorageService(ConnectionString);

        var articles = DataGenerator.GenerateArticles(count: 1000);

        _priceArticleData = articles
            .Where(a => a.Fields.ContainsKey("price"))
            .Select(a => (a.Id, (IReadOnlyList<FieldChange>)a.Fields["price"]))
            .Take(200)
            .ToList();
    }

    [Benchmark(Description = "Write packed blob (200 articles, 1 field)")]
    public byte[] WritePacked()
    {
        return PackedBlobWriter.Write(FieldType.Number, _priceArticleData);
    }

    [Benchmark(Description = "Write + Upload packed blob (200 articles, 1 field)")]
    public async Task WriteAndUploadPacked()
    {
        var blob = PackedBlobWriter.Write(FieldType.Number, _priceArticleData);
        await _storage.UploadPackedFieldBlobAsync("price-bench", blob);
    }
}
