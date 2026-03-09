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
                var optionalBlob = await _storage.DownloadFieldBlobAsync(articleId, fieldName);
                if (!optionalBlob.HasValue)
                    return;

                var blob = optionalBlob.Value;
                var changes = filter.Mode switch
                {
                    TimeFilterMode.Before => TemporalQuery.Before(blob, filter.From),
                    TimeFilterMode.After => TemporalQuery.After(blob, filter.From),
                    TimeFilterMode.Between => TemporalQuery.Between(blob, filter.From, filter.To!.Value),
                    _ => throw new ArgumentOutOfRangeException()
                };

                if (changes.Count > 0)
                    results[articleId] = changes;
            });

        return new Dictionary<Guid, List<FieldChange>>(results);
    }
}
