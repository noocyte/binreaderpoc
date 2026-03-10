using BinReader.BlobFormat;
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
        for (var i = 0; i < header.ArticleCount; i++)
        {
            var entry = ReadArticleIndex(blob, i);
            if (entry.ArticleIdHash == targetHash)
                return entry;
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
