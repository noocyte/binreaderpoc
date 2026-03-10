using BinReader.Models;
using BinReader.PackedBlobFormat;
using BinReader.Services;

namespace BinReader.Query;

public class PackedMultiArticleQuery
{
    private readonly BlobStorageService _storage;

    public PackedMultiArticleQuery(BlobStorageService storage)
    {
        _storage = storage;
    }

    public async Task<Dictionary<Guid, List<FieldChange>>> QueryAsync(
        IReadOnlyList<Guid> articleIds,
        string fieldName,
        TimeFilter filter)
    {
        var optionalBlob = await _storage.DownloadPackedFieldBlobAsync(fieldName);
        if (!optionalBlob.HasValue)
            return new Dictionary<Guid, List<FieldChange>>();

        var blob = optionalBlob.Value;
        var header = PackedBlobReader.ReadHeader(blob);
        var results = new Dictionary<Guid, List<FieldChange>>();

        for (var i = 0; i < articleIds.Count; i++)
        {
            var articleIndex = PackedBlobReader.FindArticle(blob, header, articleIds[i]);
            if (articleIndex is null)
                continue;

            var changes = ApplyFilter(blob, header, articleIndex.Value, filter);
            if (changes.Count > 0)
                results[articleIds[i]] = changes;
        }

        return results;
    }

    public async Task<Dictionary<Guid, double>> SumAsync(
        IReadOnlyList<Guid> articleIds, string fieldName, TimeFilter filter)
    {
        return await AggregateAsync(articleIds, fieldName, filter, static changes =>
        {
            var sum = 0.0;
            for (var i = 0; i < changes.Count; i++)
                sum += changes[i].RawValue;
            return sum;
        });
    }

    public async Task<Dictionary<Guid, double>> MaxAsync(
        IReadOnlyList<Guid> articleIds, string fieldName, TimeFilter filter)
    {
        return await AggregateAsync(articleIds, fieldName, filter, static changes =>
        {
            var max = double.MinValue;
            for (var i = 0; i < changes.Count; i++)
            {
                if (changes[i].RawValue > max)
                    max = changes[i].RawValue;
            }
            return max;
        });
    }

    public async Task<Dictionary<Guid, double>> MinAsync(
        IReadOnlyList<Guid> articleIds, string fieldName, TimeFilter filter)
    {
        return await AggregateAsync(articleIds, fieldName, filter, static changes =>
        {
            var min = double.MaxValue;
            for (var i = 0; i < changes.Count; i++)
            {
                if (changes[i].RawValue < min)
                    min = changes[i].RawValue;
            }
            return min;
        });
    }

    public async Task<Dictionary<Guid, double>> AvgAsync(
        IReadOnlyList<Guid> articleIds, string fieldName, TimeFilter filter)
    {
        return await AggregateAsync(articleIds, fieldName, filter, static changes =>
        {
            var sum = 0.0;
            for (var i = 0; i < changes.Count; i++)
                sum += changes[i].RawValue;
            return sum / changes.Count;
        });
    }

    private async Task<Dictionary<Guid, double>> AggregateAsync(
        IReadOnlyList<Guid> articleIds,
        string fieldName,
        TimeFilter filter,
        Func<List<FieldChange>, double> aggregate)
    {
        var optionalBlob = await _storage.DownloadPackedFieldBlobAsync(fieldName);
        if (!optionalBlob.HasValue)
            return new Dictionary<Guid, double>();

        var blob = optionalBlob.Value;
        var header = PackedBlobReader.ReadHeader(blob);
        var results = new Dictionary<Guid, double>();

        for (var i = 0; i < articleIds.Count; i++)
        {
            var articleIndex = PackedBlobReader.FindArticle(blob, header, articleIds[i]);
            if (articleIndex is null)
                continue;

            var changes = ApplyFilter(blob, header, articleIndex.Value, filter);
            if (changes.Count == 0)
                continue;

            if (changes[0].FieldType != FieldType.Number)
                throw new InvalidOperationException(
                    $"Aggregate functions only support Number fields, but got {changes[0].FieldType}.");

            results[articleIds[i]] = aggregate(changes);
        }

        return results;
    }

    private static List<FieldChange> ApplyFilter(
        ReadOnlySpan<byte> blob, PackedBlobHeader header, ArticleIndexEntry articleIndex, TimeFilter filter)
    {
        return filter.Mode switch
        {
            TimeFilterMode.Before => PackedTemporalQuery.Before(blob, header, articleIndex, filter.From),
            TimeFilterMode.After => PackedTemporalQuery.After(blob, header, articleIndex, filter.From),
            TimeFilterMode.Between => PackedTemporalQuery.Between(blob, header, articleIndex, filter.From, filter.To!.Value),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
