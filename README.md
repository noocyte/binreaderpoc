# BinReader

Proof of concept for tracking article field changes over time using Azure Blob Storage with a custom fixed-width binary format.

## Concept

Articles have fields of type `number` (double), `datetime`, or `bool`. Each field change is recorded with a timestamp. One blob per field per article stores the full change history in a compact binary format:

- **Header** (5 bytes): field type + entry count
- **Entry** (16 bytes): timestamp ticks + value as double

The fixed-width layout enables O(log n) binary search by timestamp and O(1) random access by index.

## Queries

`MultiArticleQuery` fetches the same field across a list of article IDs with a time filter:

- **Before** — all changes before a timestamp
- **After** — all changes after a timestamp
- **Between** — all changes within a time range

Downloads run concurrently via `Parallel.ForEachAsync`.

## Running

Set the connection string (defaults to Azurite if unset):

```bash
export AZURE_STORAGE_CONNECTION_STRING="your-connection-string"
```

Run the benchmarks:

```bash
dotnet run -c Release
```

This uploads 1000 generated articles (~3000 blobs) then runs BenchmarkDotNet benchmarks.

## Performance

See [PERFORMANCE.md](PERFORMANCE.md) for benchmark results comparing Azurite and Azure Blob Storage.
