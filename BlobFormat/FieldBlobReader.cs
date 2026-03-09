using BinReader.Models;

namespace BinReader.BlobFormat;

public static class FieldBlobReader
{
    public static BlobHeader ReadHeader(ReadOnlySpan<byte> blob)
    {
        var ft = (FieldType)blob[0];
        var count = BitConverter.ToInt32(blob.Slice(1, 4));
        return new BlobHeader(ft, count);
    }

    public static BlobEntry ReadEntry(ReadOnlySpan<byte> blob, int index)
    {
        var offset = BlobHeader.Size + index * BlobEntry.Size;
        var ticks = BitConverter.ToInt64(blob.Slice(offset, 8));
        var value = BitConverter.ToDouble(blob.Slice(offset + 8, 8));
        return new BlobEntry(ticks, value);
    }

    public static List<FieldChange> ReadAll(ReadOnlySpan<byte> blob)
    {
        var header = ReadHeader(blob);
        var list = new List<FieldChange>(header.EntryCount);
        for (var i = 0; i < header.EntryCount; i++)
        {
            var entry = ReadEntry(blob, i);
            list.Add(new FieldChange(
                new DateTime(entry.TimestampTicks),
                header.FieldType,
                entry.RawValue));
        }
        return list;
    }
}
