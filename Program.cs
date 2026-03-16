using System.Diagnostics;
using BenchmarkDotNet.Running;
using BinReader.Benchmarks;
using BinReader.DataGeneration;
using BinReader.Models;
using BinReader.PackedBlobFormat;
using BinReader.Services;

var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
    ?? throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING environment variable is not set.");

// --- Upload test data once before benchmarks ---
Console.WriteLine("Uploading test data to Azure Blob Storage...");
var storage = new BlobStorageService(connectionString);
await storage.InitializeAsync();

var sw = Stopwatch.StartNew();
var articles = DataGenerator.GenerateSingleFieldArticles("price", FieldType.Number,
    articleCount: 2000, minChanges: 100, maxChanges: 2000);

var articleData = articles
    .Select(a => (a.Id, (IReadOnlyList<FieldChange>)a.Fields["price"]))
    .ToList();

var totalChanges = articleData.Sum(a => a.Item2.Count);
var packedBlob = PackedBlobWriter.Write(FieldType.Number, articleData);
Console.WriteLine($"Packed blob size: {packedBlob.Length:N0} bytes ({totalChanges:N0} changes across {articles.Count} articles)");

await storage.UploadPackedFieldBlobAsync("price", packedBlob);

sw.Stop();
Console.WriteLine($"Uploaded in {sw.ElapsedMilliseconds}ms");
Console.WriteLine();

// --- Run benchmarks ---
BenchmarkRunner.Run<PackedBenchmarks>();
