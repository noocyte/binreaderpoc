using System.Collections.Concurrent;
using BinReader.Models;
using BinReader.Services;

namespace BinReader.Query;

public enum TimeFilterMode
{
    Before,
    After,
    Between
}

public record TimeFilter(TimeFilterMode Mode, DateTime From, DateTime? To = null)
{
    public static TimeFilter BeforeTime(DateTime time) => new(TimeFilterMode.Before, time);
    public static TimeFilter AfterTime(DateTime time) => new(TimeFilterMode.After, time);
    public static TimeFilter BetweenTimes(DateTime from, DateTime to) => new(TimeFilterMode.Between, from, to);
}

public class MultiArticleQuery
{
    private readonly BlobStorageService _storage;
    private readonly int _maxConcurrency;

    public MultiArticleQuery(BlobStorageService storage, int maxConcurrency = 20)
    {
        _storage = storage;
        _maxConcurrency = maxConcurrency;
    }

    public async Task<Dictionary<Guid, List<FieldChange>>> QueryAsync(
        IReadOnlyList<Guid> articleIds,
        string fieldName,
        TimeFilter filter)
    {
        var results = new ConcurrentDictionary<Guid, List<FieldChange>>();

        await Parallel.ForEachAsync(
            articleIds,
            new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency },
            async (articleId, ct) =>
            {
                var changes = await QuerySingleArticleAsync(articleId, fieldName, filter);
                if (changes is not null)
                    results[articleId] = changes;
            });

        return new Dictionary<Guid, List<FieldChange>>(results);
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
        var results = new ConcurrentDictionary<Guid, double>();

        await Parallel.ForEachAsync(
            articleIds,
            new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency },
            async (articleId, ct) =>
            {
                var changes = await QuerySingleArticleAsync(articleId, fieldName, filter);
                if (changes is null)
                    return;

                if (changes[0].FieldType != FieldType.Number)
                    throw new InvalidOperationException(
                        $"Aggregate functions only support Number fields, but got {changes[0].FieldType}.");

                results[articleId] = aggregate(changes);
            });

        return new Dictionary<Guid, double>(results);
    }

    private async Task<List<FieldChange>?> QuerySingleArticleAsync(
        Guid articleId, string fieldName, TimeFilter filter)
    {
        var optionalBlob = await _storage.DownloadFieldBlobAsync(articleId, fieldName);
        if (!optionalBlob.HasValue)
            return null;

        var blob = optionalBlob.Value;
        var changes = filter.Mode switch
        {
            TimeFilterMode.Before => TemporalQuery.Before(blob, filter.From),
            TimeFilterMode.After => TemporalQuery.After(blob, filter.From),
            TimeFilterMode.Between => TemporalQuery.Between(blob, filter.From, filter.To!.Value),
            _ => throw new ArgumentOutOfRangeException()
        };

        return changes.Count > 0 ? changes : null;
    }
}
