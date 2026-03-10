using BinReader.Models;

namespace BinReader.PackedBlobFormat;

public static class PackedBlobWriter
{
    public static byte[] Write(
        FieldType fieldType,
        IReadOnlyList<(Guid ArticleId, IReadOnlyList<FieldChange> Changes)> articles)
    {
        var valueSize = PackedBlobHeader.GetValueSize(fieldType);
        var entrySize = 8 + valueSize;

        // Calculate total entries to size the buffer
        var totalEntries = 0;
        for (var i = 0; i < articles.Count; i++)
            totalEntries += articles[i].Changes.Count;

        var bufferSize = PackedBlobHeader.Size
            + articles.Count * ArticleIndexEntry.Size
            + totalEntries * entrySize;
        var buffer = new byte[bufferSize];

        // Write header
        buffer[0] = (byte)fieldType;
        buffer[1] = valueSize;
        BitConverter.TryWriteBytes(buffer.AsSpan(2, 4), articles.Count);

        // Write article index and entries
        var entryByteOffset = 0;
        var entriesSectionStart = PackedBlobHeader.Size + articles.Count * ArticleIndexEntry.Size;

        for (var i = 0; i < articles.Count; i++)
        {
            var (articleId, changes) = articles[i];

            // Write index entry
            var indexOffset = PackedBlobHeader.Size + i * ArticleIndexEntry.Size;
            BitConverter.TryWriteBytes(buffer.AsSpan(indexOffset, 8), ArticleIdHasher.Hash(articleId));
            BitConverter.TryWriteBytes(buffer.AsSpan(indexOffset + 8, 4), entryByteOffset);
            BitConverter.TryWriteBytes(buffer.AsSpan(indexOffset + 12, 4), changes.Count);

            // Write entries
            for (var j = 0; j < changes.Count; j++)
            {
                var offset = entriesSectionStart + entryByteOffset + j * entrySize;
                BitConverter.TryWriteBytes(buffer.AsSpan(offset, 8), changes[j].Timestamp.Ticks);

                if (fieldType == FieldType.Bool)
                    buffer[offset + 8] = (byte)(changes[j].RawValue != 0 ? 1 : 0);
                else
                    BitConverter.TryWriteBytes(buffer.AsSpan(offset + 8, 8), changes[j].RawValue);
            }

            entryByteOffset += changes.Count * entrySize;
        }

        return buffer;
    }
}
