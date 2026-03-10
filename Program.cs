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
var articles = DataGenerator.GenerateArticles(count: 1000);

// --- Upload packed blobs (one per field) ---
var fieldGroups = new Dictionary<string, (FieldType Type, List<(Guid ArticleId, IReadOnlyList<FieldChange> Changes)> Articles)>();

foreach (var article in articles)
{
    foreach (var (fieldName, changes) in article.Fields)
    {
        if (!fieldGroups.ContainsKey(fieldName))
            fieldGroups[fieldName] = (changes[0].FieldType, new List<(Guid, IReadOnlyList<FieldChange>)>());
        fieldGroups[fieldName].Articles.Add((article.Id, changes));
    }
}

var packedCount = 0;
foreach (var (fieldName, (fieldType, articleData)) in fieldGroups)
{
    var packedBlob = PackedBlobWriter.Write(fieldType, articleData);
    await storage.UploadPackedFieldBlobAsync(fieldName, packedBlob);
    packedCount++;
}

sw.Stop();
Console.WriteLine($"Uploaded {packedCount} packed field blobs in {sw.ElapsedMilliseconds}ms");
Console.WriteLine();

// --- Run benchmarks ---
BenchmarkRunner.Run<PackedWriteBenchmarks>();
BenchmarkRunner.Run<PackedMultiArticleQueryBenchmarks>();
