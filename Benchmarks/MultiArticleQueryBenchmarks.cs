using BenchmarkDotNet.Attributes;
using BinReader.DataGeneration;
using BinReader.Models;
using BinReader.Query;
using BinReader.Services;

namespace BinReader.Benchmarks;

[SimpleJob(warmupCount: 1, iterationCount: 3)]
[MemoryDiagnoser]
public class MultiArticleQueryBenchmarks
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
        ?? "DefaultEndpointsProtocol=http;" +
           "AccountName=devstoreaccount1;" +
           "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/" +
           "K1SZFPTOtr/KBHBeksoGMGw==;" +
           "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    private MultiArticleQuery _query = null!;
    private List<Guid> _priceArticleIds = null!;
    private TimeFilter _betweenFilter = null!;
    private TimeFilter _beforeFilter = null!;
    private TimeFilter _afterFilter = null!;

    [Params(10, 50)]
    public int ArticleCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Data is pre-uploaded by Program.cs before the benchmark runs.
        // We just need to set up the query objects.
        var storage = new BlobStorageService(ConnectionString);
        _query = new MultiArticleQuery(storage);

        // Regenerate the same articles (deterministic seed) to get the IDs
        var articles = DataGenerator.GenerateArticles(count: 1000);

        _priceArticleIds = articles
            .Where(a => a.Fields.ContainsKey("price"))
            .Select(a => a.Id)
            .ToList();

        _betweenFilter = TimeFilter.BetweenTimes(
            new DateTime(2021, 6, 1),
            new DateTime(2022, 6, 1));

        _beforeFilter = TimeFilter.BeforeTime(new DateTime(2022, 6, 1));
        _afterFilter = TimeFilter.AfterTime(new DateTime(2022, 1, 1));
    }

    private List<Guid> GetArticleSubset() => _priceArticleIds.Take(ArticleCount).ToList();

    [Benchmark(Description = "Between query")]
    public async Task<Dictionary<Guid, List<FieldChange>>> QueryBetween()
    {
        return await _query.QueryAsync(GetArticleSubset(), "price", _betweenFilter);
    }

    [Benchmark(Description = "Before query")]
    public async Task<Dictionary<Guid, List<FieldChange>>> QueryBefore()
    {
        return await _query.QueryAsync(GetArticleSubset(), "price", _beforeFilter);
    }

    [Benchmark(Description = "After query")]
    public async Task<Dictionary<Guid, List<FieldChange>>> QueryAfter()
    {
        return await _query.QueryAsync(GetArticleSubset(), "price", _afterFilter);
    }

    [Benchmark(Description = "Between query + Sum")]
    public async Task<Dictionary<Guid, double>> QueryBetweenWithSum()
    {
        return await _query.SumAsync(GetArticleSubset(), "price", _betweenFilter);
    }
}
