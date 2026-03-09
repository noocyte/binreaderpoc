using System.Diagnostics;
using BenchmarkDotNet.Running;
using BinReader.Benchmarks;
using BinReader.BlobFormat;
using BinReader.DataGeneration;
using BinReader.Services;

var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
    ?? "DefaultEndpointsProtocol=http;" +
       "AccountName=devstoreaccount1;" +
       "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/" +
       "K1SZFPTOtr/KBHBeksoGMGw==;" +
       "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

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
Console.WriteLine($"Uploaded {blobCount} blobs in {sw.ElapsedMilliseconds}ms");
Console.WriteLine();

// --- Run benchmarks ---
BenchmarkRunner.Run<MultiArticleQueryBenchmarks>();
