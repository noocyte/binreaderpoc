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
        var results = new Dictionary<Guid, List<FieldChange>>();
        var shardGroups = GroupByShard(articleIds);

        var tasks = shardGroups.Select(async group =>
        {
            var optionalBlob = await _storage.DownloadPackedFieldBlobAsync(fieldName, group.Key);
            if (!optionalBlob.HasValue)
                return new List<(Guid Id, List<FieldChange> Changes)>();

            var blob = optionalBlob.Value;
            var header = PackedBlobReader.ReadHeader(blob);
            var shardResults = new List<(Guid Id, List<FieldChange> Changes)>();

            foreach (var id in group.Value)
            {
                var articleIndex = PackedBlobReader.FindArticle(blob, header, id);
                if (articleIndex is null)
                    continue;

                var changes = ApplyFilter(blob, header, articleIndex.Value, filter);
                if (changes.Count > 0)
                    shardResults.Add((id, changes));
            }

            return shardResults;
        }).ToList();

        foreach (var shardResults in await Task.WhenAll(tasks))
            foreach (var (id, changes) in shardResults)
                results[id] = changes;

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
        var results = new Dictionary<Guid, double>();
        var shardGroups = GroupByShard(articleIds);

        var tasks = shardGroups.Select(async group =>
        {
            var optionalBlob = await _storage.DownloadPackedFieldBlobAsync(fieldName, group.Key);
            if (!optionalBlob.HasValue)
                return new List<(Guid Id, double Value)>();

            var blob = optionalBlob.Value;
            var header = PackedBlobReader.ReadHeader(blob);
            var shardResults = new List<(Guid Id, double Value)>();

            foreach (var id in group.Value)
            {
                var articleIndex = PackedBlobReader.FindArticle(blob, header, id);
                if (articleIndex is null)
                    continue;

                var changes = ApplyFilter(blob, header, articleIndex.Value, filter);
                if (changes.Count == 0)
                    continue;

                if (changes[0].FieldType != FieldType.Number)
                    throw new InvalidOperationException(
                        $"Aggregate functions only support Number fields, but got {changes[0].FieldType}.");

                shardResults.Add((id, aggregate(changes)));
            }

            return shardResults;
        }).ToList();

        foreach (var shardResults in await Task.WhenAll(tasks))
            foreach (var (id, value) in shardResults)
                results[id] = value;

        return results;
    }

    private static Dictionary<char, List<Guid>> GroupByShard(IReadOnlyList<Guid> articleIds)
    {
        var groups = new Dictionary<char, List<Guid>>();
        for (var i = 0; i < articleIds.Count; i++)
        {
            var shard = ShardKey.ForGuid(articleIds[i]);
            if (!groups.TryGetValue(shard, out var list))
            {
                list = new List<Guid>();
                groups[shard] = list;
            }
            list.Add(articleIds[i]);
        }
        return groups;
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
