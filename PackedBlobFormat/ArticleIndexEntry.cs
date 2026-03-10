namespace BinReader.PackedBlobFormat;

public readonly record struct ArticleIndexEntry(ulong ArticleIdHash, int EntryOffset, int EntryCount)
{
    public const int Size = 16; // 8 bytes hash + 4 bytes offset + 4 bytes count
}
