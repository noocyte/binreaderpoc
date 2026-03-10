using System.Diagnostics;
using BenchmarkDotNet.Running;
using BinReader.Benchmarks;
using BinReader.BlobFormat;
using BinReader.DataGeneration;
using BinReader.Models;
using BinReader.PackedBlobFormat;
using BinReader.Services;

var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
    // ?? "DefaultEndpointsProtocol=http;" +
    //    "AccountName=devstoreaccount1;" +
    //    "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/" +
    //    "K1SZFPTOtr/KBHBeksoGMGw==;" +
    //    "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

// --- Upload test data once before benchmarks ---
var isAzurite = connectionString.Contains("devstoreaccount1");
Console.WriteLine($"Uploading test data to {(isAzurite ? "Azurite" : "Azure Blob Storage")}...");
var storage = new BlobStorageService(connectionString);
await storage.InitializeAsync();

var sw = Stopwatch.StartNew();
var articles = DataGenerator.GenerateArticles(count: 1000);
var blobCount = 0;

foreach (var article in articles)
{
    foreach (var (fieldName, changes) in article.Fields)
    {
        var blob = FieldBlobWriter.Write(changes[0].FieldType, changes);
        await storage.UploadFieldBlobAsync(article.Id, fieldName, blob);
        blobCount++;
    }
}

sw.Stop();
Console.WriteLine($"Uploaded {blobCount} per-article blobs in {sw.ElapsedMilliseconds}ms");

// --- Upload packed blobs (one per field) ---
sw.Restart();
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
BenchmarkRunner.Run<MultiArticleQueryBenchmarks>();
BenchmarkRunner.Run<PackedMultiArticleQueryBenchmarks>();
