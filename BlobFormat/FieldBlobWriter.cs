using BinReader.Models;

namespace BinReader.BlobFormat;

public static class FieldBlobWriter
{
    public static byte[] Write(FieldType fieldType, IReadOnlyList<FieldChange> changes)
    {
        var entryCount = changes.Count;
        var buffer = new byte[BlobHeader.Size + entryCount * BlobEntry.Size];

        buffer[0] = (byte)fieldType;
        BitConverter.TryWriteBytes(buffer.AsSpan(1, 4), entryCount);

        for (var i = 0; i < entryCount; i++)
        {
            var offset = BlobHeader.Size + i * BlobEntry.Size;
            BitConverter.TryWriteBytes(buffer.AsSpan(offset, 8), changes[i].Timestamp.Ticks);
            BitConverter.TryWriteBytes(buffer.AsSpan(offset + 8, 8), changes[i].RawValue);
        }

        return buffer;
    }
}
