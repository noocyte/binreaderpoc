using BinReader.Models;

namespace BinReader.PackedBlobFormat;

public static class PackedBlobReader
{
    public static PackedBlobHeader ReadHeader(ReadOnlySpan<byte> blob)
    {
        var fieldType = (FieldType)blob[0];
        var valueSize = blob[1];
        var articleCount = BitConverter.ToInt32(blob.Slice(2, 4));
        return new PackedBlobHeader(fieldType, valueSize, articleCount);
    }

    public static ArticleIndexEntry ReadArticleIndex(ReadOnlySpan<byte> blob, int index)
    {
        var offset = PackedBlobHeader.Size + index * ArticleIndexEntry.Size;
        var hash = BitConverter.ToUInt64(blob.Slice(offset, 8));
        var entryOffset = BitConverter.ToInt32(blob.Slice(offset + 8, 4));
        var entryCount = BitConverter.ToInt32(blob.Slice(offset + 12, 4));
        return new ArticleIndexEntry(hash, entryOffset, entryCount);
    }

    public static ArticleIndexEntry? FindArticle(
        ReadOnlySpan<byte> blob, PackedBlobHeader header, Guid articleId)
    {
        var targetHash = ArticleIdHasher.Hash(articleId);
        int lo = 0, hi = header.ArticleCount - 1;
        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            var entry = ReadArticleIndex(blob, mid);
            if (entry.ArticleIdHash == targetHash)
                return entry;
            if (entry.ArticleIdHash < targetHash)
                lo = mid + 1;
            else
                hi = mid - 1;
        }
        return null;
    }

    public static BlobEntry ReadEntry(
        ReadOnlySpan<byte> blob, PackedBlobHeader header, int entriesSectionStart, int entryByteOffset)
    {
        var offset = entriesSectionStart + entryByteOffset;
        var ticks = BitConverter.ToInt64(blob.Slice(offset, 8));
        double value = header.ValueSize == 1
            ? blob[offset + 8]
            : BitConverter.ToDouble(blob.Slice(offset + 8, 8));
        return new BlobEntry(ticks, value);
    }

    public static List<FieldChange> ReadArticleEntries(
        ReadOnlySpan<byte> blob, PackedBlobHeader header, ArticleIndexEntry index)
    {
        var entriesSectionStart = PackedBlobHeader.Size + header.ArticleCount * ArticleIndexEntry.Size;
        var entrySize = 8 + header.ValueSize;
        var result = new List<FieldChange>(index.EntryCount);

        for (var i = 0; i < index.EntryCount; i++)
        {
            var entry = ReadEntry(blob, header, entriesSectionStart, index.EntryOffset + i * entrySize);
            result.Add(new FieldChange(
                new DateTime(entry.TimestampTicks),
                header.FieldType,
                entry.RawValue));
        }

        return result;
    }
}
