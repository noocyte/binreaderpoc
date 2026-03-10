namespace BinReader.PackedBlobFormat;

public readonly record struct BlobEntry(long TimestampTicks, double RawValue)
{
    public const int Size = 16; // 8 bytes timestamp + 8 bytes value
}
