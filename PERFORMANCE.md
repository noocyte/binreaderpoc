# Performance Benchmarks

Benchmarks run with [BenchmarkDotNet](https://benchmarkdotnet.org/) against Azure Blob Storage (Standard v2).

Test data: 2000 articles with a single "price" field, 100–2000 changes each (~2.1M total changes across 16 shards). Shards average ~2.1 MB uncompressed. Time filter: one year range (June 2021–June 2022).

## Results — Azure VM (same region as storage)

Run on a `Standard_D2s_v5` spot VM colocated with the storage account, eliminating network latency.

| Method                   | ArticleCount | Mean     | StdDev   | Allocated  |
|------------------------- |------------- |---------:|---------:|-----------:|
| Write + Upload packed    | 100          |  192 ms  |  15 ms   |   4.61 MB  |
| Query: Between + Sum     | 100          |  175 ms  |  21 ms   | 149.54 MB  |
| Write + Upload packed    | 500          |  477 ms  |  21 ms   |  21.95 MB  |
| Query: Between + Sum     | 500          |  171 ms  |  15 ms   | 149.60 MB  |

**Environment:** Intel Xeon Platinum 8370C 2.80 GHz, 2 vCPU, .NET 10.0.5, Linux Ubuntu 24.04

## Results — WSL2 (remote from storage)

Run from a local WSL2 instance connecting to Azure over the internet.

| Method                   | ArticleCount | Mean       | StdDev    | Allocated  |
|------------------------- |------------- |-----------:|----------:|-----------:|
| Write + Upload packed    | 100          |  1,618 ms  |  42 ms   |   4.61 MB  |
| Query: Between + Sum     | 100          |    853 ms  |  42 ms   | 149.75 MB  |
| Write + Upload packed    | 500          |  2,080 ms  | 127 ms   |  21.96 MB  |
| Query: Between + Sum     | 500          |  1,114 ms  |  51 ms   | 149.84 MB  |

**Environment:** AMD Ryzen 7 7700X, .NET 10.0.4, Linux (WSL2)

## Network latency impact

| Method                   | ArticleCount | WSL2     | Azure VM | Speedup |
|------------------------- |------------- |---------:|---------:|--------:|
| Write + Upload packed    | 100          | 1,618 ms |   192 ms |   8.4x  |
| Query: Between + Sum     | 100          |   853 ms |   175 ms |   4.9x  |
| Write + Upload packed    | 500          | 2,080 ms |   477 ms |   4.4x  |
| Query: Between + Sum     | 500          | 1,114 ms |   171 ms |   6.5x  |

Network latency accounts for **75–90%** of total time when running remotely. The actual compute + storage I/O (serialization, compression, blob read/write) is fast — queries complete in ~170 ms regardless of article count when colocated.

## Observations

- **Network latency dominates remote runs**: 4–8x speedup by colocating with storage, confirming most time was spent on round-trips rather than compute.
- **Query scaling is sub-linear**: 5x more articles (100 → 500) costs ~1.3x more time remotely, and essentially no extra time colocated — articles within the same shard share a single blob download.
- **Write scaling is closer to linear**: 5x more articles costs ~2.5x more time even colocated, reflecting serialization + upload of more/larger shards.
- **Query memory is dominated by HTTP buffers**: ~150 MB allocated regardless of article count, driven by downloading and decompressing all 16 shard blobs.
- **Write memory scales with article count**: 4.6 MB for 100 articles vs 22 MB for 500, reflecting serialization buffer sizes.
- **Binary search overhead is negligible**: the O(log n) article lookup and temporal range search add virtually nothing compared to the blob download + decompression cost.

## Configuration

- Azure.Storage.Blobs 12.22.2
- ZstdSharp.Port 0.8.7 (Zstandard compression, level 7)
- BenchmarkDotNet 0.15.8 with `SimpleJob(warmupCount: 1, iterationCount: 3)`
- 2000 articles, 100–2000 changes per article (deterministic seed)
- 16 shards (GUID hex prefix), ~2.1 MB per shard uncompressed
