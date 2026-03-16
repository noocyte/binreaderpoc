# Performance Benchmarks

Benchmarks run with [BenchmarkDotNet](https://benchmarkdotnet.org/) against Azure Blob Storage (Standard v2).

Test data: 2000 articles with a single "price" field, 100–2000 changes each (~2.1M total changes across 16 shards). Shards average ~2.1 MB uncompressed. Time filter: one year range (June 2021–June 2022).

## Results

| Method                   | ArticleCount | Mean       | Error      | StdDev    | Allocated  |
|------------------------- |------------- |-----------:|-----------:|----------:|-----------:|
| Write + Upload packed    | 100          |  1,617.6 ms |   766.6 ms |  42.02 ms |   4.61 MB |
| Query: Between + Sum     | 100          |    853.1 ms |   758.7 ms |  41.59 ms | 149.75 MB |
| Write + Upload packed    | 500          |  2,079.6 ms | 2,312.5 ms | 126.76 ms |  21.96 MB |
| Query: Between + Sum     | 500          |  1,113.7 ms |   923.3 ms |  50.61 ms | 149.84 MB |

## Observations

- **Query scaling is sub-linear**: 5x more articles (100 → 500) costs ~1.3x more time, because articles within the same shard share a single blob download.
- **Write scaling is closer to linear**: 5x more articles costs ~1.3x more time for serialization + upload across more/larger shards.
- **Query memory is dominated by HTTP buffers**: ~150 MB allocated regardless of article count, driven by downloading and decompressing all 16 shard blobs.
- **Write memory scales with article count**: 4.6 MB for 100 articles vs 22 MB for 500, reflecting serialization buffer sizes.
- **Binary search overhead is negligible**: the O(log n) article lookup and temporal range search add virtually nothing compared to the blob download + decompression cost.

## Configuration

- .NET 10.0.4, Linux (WSL2), AMD Ryzen 7 7700X
- Azure.Storage.Blobs 12.22.2
- ZstdSharp.Port 0.8.7 (Zstandard compression, level 7)
- BenchmarkDotNet 0.15.8 with `SimpleJob(warmupCount: 1, iterationCount: 3)`
- 2000 articles, 100–2000 changes per article (deterministic seed)
- 16 shards (GUID hex prefix), ~2.1 MB per shard uncompressed
