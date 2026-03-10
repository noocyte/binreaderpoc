using BinReader.Models;
using BinReader.PackedBlobFormat;

namespace BinReader.Query;

public static class PackedTemporalQuery
{
    public static List<FieldChange> Before(
        ReadOnlySpan<byte> blob, PackedBlobHeader header, ArticleIndexEntry articleIndex, DateTime time)
    {
        if (articleIndex.EntryCount == 0)
            return [];

        var entriesSectionStart = PackedBlobHeader.Size + header.ArticleCount * ArticleIndexEntry.Size;
        var entrySize = 8 + header.ValueSize;

        var lastIndex = UpperBound(blob, header, entriesSectionStart, articleIndex, entrySize, time.Ticks) - 1;
        if (lastIndex < 0)
            return [];

        return ReadRange(blob, header, entriesSectionStart, articleIndex, entrySize, 0, lastIndex);
    }

    public static List<FieldChange> After(
        ReadOnlySpan<byte> blob, PackedBlobHeader header, ArticleIndexEntry articleIndex, DateTime time)
    {
        if (articleIndex.EntryCount == 0)
            return [];

        var entriesSectionStart = PackedBlobHeader.Size + header.ArticleCount * ArticleIndexEntry.Size;
        var entrySize = 8 + header.ValueSize;

        var firstIndex = LowerBound(blob, header, entriesSectionStart, articleIndex, entrySize, time.Ticks);
        if (firstIndex >= articleIndex.EntryCount)
            return [];

        return ReadRange(blob, header, entriesSectionStart, articleIndex, entrySize, firstIndex, articleIndex.EntryCount - 1);
    }

    public static List<FieldChange> Between(
        ReadOnlySpan<byte> blob, PackedBlobHeader header, ArticleIndexEntry articleIndex, DateTime from, DateTime to)
    {
        if (articleIndex.EntryCount == 0)
            return [];

        var entriesSectionStart = PackedBlobHeader.Size + header.ArticleCount * ArticleIndexEntry.Size;
        var entrySize = 8 + header.ValueSize;

        var firstIndex = LowerBound(blob, header, entriesSectionStart, articleIndex, entrySize, from.Ticks);
        var lastIndex = UpperBound(blob, header, entriesSectionStart, articleIndex, entrySize, to.Ticks) - 1;

        if (firstIndex > lastIndex)
            return [];

        return ReadRange(blob, header, entriesSectionStart, articleIndex, entrySize, firstIndex, lastIndex);
    }

    private static int LowerBound(
        ReadOnlySpan<byte> blob, PackedBlobHeader header,
        int entriesSectionStart, ArticleIndexEntry articleIndex, int entrySize, long targetTicks)
    {
        int lo = 0, hi = articleIndex.EntryCount;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            var entry = PackedBlobReader.ReadEntry(blob, header, entriesSectionStart, articleIndex.EntryOffset + mid * entrySize);
            if (entry.TimestampTicks < targetTicks)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    private static int UpperBound(
        ReadOnlySpan<byte> blob, PackedBlobHeader header,
        int entriesSectionStart, ArticleIndexEntry articleIndex, int entrySize, long targetTicks)
    {
        int lo = 0, hi = articleIndex.EntryCount;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            var entry = PackedBlobReader.ReadEntry(blob, header, entriesSectionStart, articleIndex.EntryOffset + mid * entrySize);
            if (entry.TimestampTicks <= targetTicks)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    private static List<FieldChange> ReadRange(
        ReadOnlySpan<byte> blob, PackedBlobHeader header,
        int entriesSectionStart, ArticleIndexEntry articleIndex, int entrySize,
        int startIndex, int endIndex)
    {
        var result = new List<FieldChange>(endIndex - startIndex + 1);
        for (var i = startIndex; i <= endIndex; i++)
        {
            var entry = PackedBlobReader.ReadEntry(blob, header, entriesSectionStart, articleIndex.EntryOffset + i * entrySize);
            result.Add(new FieldChange(
                new DateTime(entry.TimestampTicks),
                header.FieldType,
                entry.RawValue));
        }
        return result;
    }
}
