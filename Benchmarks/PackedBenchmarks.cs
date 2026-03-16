using BenchmarkDotNet.Attributes;
using BinReader.DataGeneration;
using BinReader.Models;
using BinReader.PackedBlobFormat;
using BinReader.Query;
using BinReader.Services;

namespace BinReader.Benchmarks;

[SimpleJob(warmupCount: 1, iterationCount: 3)]
[MemoryDiagnoser]
public class PackedBenchmarks
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")!;

    private BlobStorageService _storage = null!;
    private PackedMultiArticleQuery _query = null!;
    private List<(Guid ArticleId, IReadOnlyList<FieldChange> Changes)> _priceArticleData = null!;
    private List<Guid> _priceArticleIds = null!;
    private TimeFilter _betweenFilter = null!;

    [Params(100, 500)]
    public int QueryArticleCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _storage = new BlobStorageService(ConnectionString);
        _query = new PackedMultiArticleQuery(_storage);

        var articles = DataGenerator.GenerateSingleFieldArticles("price", FieldType.Number,
            articleCount: 2000, minChanges: 100, maxChanges: 2000);

        _priceArticleData = articles
            .Select(a => (a.Id, (IReadOnlyList<FieldChange>)a.Fields["price"]))
            .ToList();

        _priceArticleIds = articles.Select(a => a.Id).ToList();

        _betweenFilter = TimeFilter.BetweenTimes(
            new DateTime(2021, 6, 1),
            new DateTime(2022, 6, 1));
    }

    private List<Guid> GetArticleSubset() => _priceArticleIds.Take(QueryArticleCount).ToList();

    // --- Write benchmarks ---

    [Benchmark(Description = "Write + Upload packed blob")]
    public async Task WriteAndUploadPacked()
    {
        var blob = PackedBlobWriter.Write(FieldType.Number, _priceArticleData.Take(QueryArticleCount).ToList());
        await _storage.UploadPackedFieldBlobAsync("price-bench", blob);
    }

    // --- Query benchmarks ---

    [Benchmark(Description = "Query: Between + Sum")]
    public async Task<Dictionary<Guid, double>> QueryBetweenWithSum()
    {
        return await _query.SumAsync(GetArticleSubset(), "price", _betweenFilter);
    }
}
