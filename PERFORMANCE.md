# Performance Benchmarks

Benchmarks run with [BenchmarkDotNet](https://benchmarkdotnet.org/) querying the `price` field across multiple articles using `MultiArticleQuery` with `Parallel.ForEachAsync` (max concurrency 20).

Each article's field blob is ~85–325 bytes (5-byte header + 5–20 entries × 16 bytes each).

## Azure Blob Storage (Premium BlockBlobStorage)

| Method          | ArticleCount | Mean      | Error     | StdDev   | Allocated |
|---------------- |------------- |----------:|----------:|---------:|----------:|
| Between query   | 10           |  33.83 ms |  2.204 ms | 0.121 ms | 140.32 KB |
| Before query    | 10           |  33.41 ms |  3.563 ms | 0.195 ms | 140.12 KB |
| After query     | 10           |  33.60 ms |  4.121 ms | 0.226 ms | 139.91 KB |
| Between query   | 50           |  97.98 ms |  7.448 ms | 0.408 ms | 686.04 KB |
| Before query    | 50           | 100.64 ms | 85.261 ms | 4.673 ms | 685.67 KB |
| After query     | 50           |  98.54 ms |  6.542 ms | 0.359 ms | 686.01 KB |

## Azurite (Local Emulator)

| Method          | ArticleCount | Mean       | Error      | StdDev    | Allocated |
|---------------- |------------- |-----------:|-----------:|----------:|----------:|
| Between query   | 10           |  289.01 ms |  42.038 ms |  2.304 ms | 141.11 KB |
| Before query    | 10           |  286.85 ms |   8.498 ms |  0.466 ms | 140.73 KB |
| After query     | 10           |  282.42 ms |  58.,56 ms |  3.207 ms | 140.66 KB |
| Between query   | 50           | 1,418.28 ms | 162.878 ms | 8.929 ms | 688.06 KB |
| Before query    | 50           | 1,406.83 ms | 116.928 ms | 6.410 ms | 688.92 KB |
| After query     | 50           | 1,397.10 ms | 202.716 ms | 11.111 ms | 688.39 KB |

## Observations

- **Azure is 8–15x faster than Azurite** for these queries, primarily due to Azurite's per-request overhead on localhost.
- **Scaling is sub-linear**: 5x more articles costs ~3x more time on Azure, thanks to connection pooling and concurrent downloads.
- **Query type has negligible impact**: Before, After, and Between all perform similarly since the binary search is O(log n) on tiny blobs — the HTTP round-trip dominates.
- **Memory allocation** is ~14 KB per article, consistent across query types.
- **Blob format**: Custom fixed-width binary (5-byte header + 16-byte entries) enables O(log n) binary search with O(1) random access by offset.

## Configuration

- .NET 10.0
- Azure.Storage.Blobs 12.22.2
- BenchmarkDotNet 0.15.8 with `SimpleJob(warmupCount: 1, iterationCount: 3)`
- 1000 articles, 2–4 fields each, 5–20 changes per field (deterministic seed)
- Max download concurrency: 20 (`Parallel.ForEachAsync`)
