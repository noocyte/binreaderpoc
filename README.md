# BinReader

Proof of concept for tracking article field changes over time using Azure Blob Storage with a custom fixed-width binary format.

## Concept

Articles have fields of type `number` (double), `datetime`, or `bool`. Each field change is recorded with a timestamp. Field data is stored in a **packed blob format** — one blob per field per shard — using Zstd compression and binary search indexes for efficient lookups.

### Binary format

Each packed blob contains all articles for a given field within a single shard:

- **Header** (6 bytes): field type (1) + value size (1) + article count (4)
- **Article index** (16 bytes per article): XxHash3 of article ID (8) + entry offset (4) + entry count (4)
- **Entries** (16 bytes each for number/datetime, 9 for bool): timestamp ticks (8) + value

The article index is sorted by hash, enabling O(log n) binary search to locate an article. Within each article's entries, timestamps are ordered, allowing O(log n) binary search for time-range queries.

### Sharding

Articles are sharded by the first hex character of their GUID into 16 shards (`0`–`f`). Each shard is stored as a separate blob at `packed/{fieldName}/{shardChar}.bin`. Queries download only the shards containing the requested article IDs.

### Compression

Blobs are compressed with Zstandard (level 7) before upload and decompressed on download.

## Queries

`PackedMultiArticleQuery` fetches the same field across a list of article IDs with a time filter:

- **Before** — all changes before a timestamp
- **After** — all changes on or after a timestamp
- **Between** — all changes within a time range (inclusive)

Shard blobs are downloaded concurrently via `Task.WhenAll`. Per-article aggregate functions are available for numeric fields: **Sum**, **Max**, **Min**, **Avg**.

## Running

Set the connection string:

```bash
export AZURE_STORAGE_CONNECTION_STRING="your-connection-string"
```

Run the benchmarks:

```bash
dotnet run -c Release
```

This generates 2000 articles (100–2000 changes each), uploads them across 16 shards, then runs BenchmarkDotNet benchmarks.

## Performance

See [PERFORMANCE.md](PERFORMANCE.md) for benchmark results.
