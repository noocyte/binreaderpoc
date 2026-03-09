using BinReader.BlobFormat;
using BinReader.Models;

namespace BinReader.Query;

public static class TemporalQuery
{
    /// <summary>
    /// Returns all entries with timestamp &lt;= the given time.
    /// </summary>
    public static List<FieldChange> Before(ReadOnlySpan<byte> blob, DateTime time)
    {
        var header = FieldBlobReader.ReadHeader(blob);
        if (header.EntryCount == 0)
            return [];

        // Find the last index where timestamp <= time
        var lastIndex = UpperBound(blob, header.EntryCount, time.Ticks) - 1;
        if (lastIndex < 0)
            return [];

        return ReadRange(blob, header.FieldType, 0, lastIndex);
    }

    /// <summary>
    /// Returns all entries with timestamp &gt;= the given time.
    /// </summary>
    public static List<FieldChange> After(ReadOnlySpan<byte> blob, DateTime time)
    {
        var header = FieldBlobReader.ReadHeader(blob);
        if (header.EntryCount == 0)
            return [];

        // Find the first index where timestamp >= time
        var firstIndex = LowerBound(blob, header.EntryCount, time.Ticks);
        if (firstIndex >= header.EntryCount)
            return [];

        return ReadRange(blob, header.FieldType, firstIndex, header.EntryCount - 1);
    }

    /// <summary>
    /// Returns all entries with timestamp &gt;= from and &lt;= to.
    /// </summary>
    public static List<FieldChange> Between(ReadOnlySpan<byte> blob, DateTime from, DateTime to)
    {
        var header = FieldBlobReader.ReadHeader(blob);
        if (header.EntryCount == 0)
            return [];

        var firstIndex = LowerBound(blob, header.EntryCount, from.Ticks);
        var lastIndex = UpperBound(blob, header.EntryCount, to.Ticks) - 1;

        if (firstIndex > lastIndex)
            return [];

        return ReadRange(blob, header.FieldType, firstIndex, lastIndex);
    }

    /// <summary>
    /// Returns the index of the first entry with timestamp &gt;= targetTicks.
    /// If all entries are less, returns entryCount.
    /// </summary>
    private static int LowerBound(ReadOnlySpan<byte> blob, int entryCount, long targetTicks)
    {
        int lo = 0, hi = entryCount;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            var entry = FieldBlobReader.ReadEntry(blob, mid);
            if (entry.TimestampTicks < targetTicks)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    /// <summary>
    /// Returns the index past the last entry with timestamp &lt;= targetTicks.
    /// i.e. the first entry with timestamp &gt; targetTicks.
    /// </summary>
    private static int UpperBound(ReadOnlySpan<byte> blob, int entryCount, long targetTicks)
    {
        int lo = 0, hi = entryCount;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            var entry = FieldBlobReader.ReadEntry(blob, mid);
            if (entry.TimestampTicks <= targetTicks)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    private static List<FieldChange> ReadRange(
        ReadOnlySpan<byte> blob, FieldType fieldType, int startIndex, int endIndex)
    {
        var result = new List<FieldChange>(endIndex - startIndex + 1);
        for (var i = startIndex; i <= endIndex; i++)
        {
            var entry = FieldBlobReader.ReadEntry(blob, i);
            result.Add(new FieldChange(
                new DateTime(entry.TimestampTicks),
                fieldType,
                entry.RawValue));
        }
        return result;
    }
}
