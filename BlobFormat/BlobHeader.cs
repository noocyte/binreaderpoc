namespace BinReader.BlobFormat;

public readonly record struct BlobHeader(Models.FieldType FieldType, int EntryCount)
{
    public const int Size = 5; // 1 byte FieldType + 4 bytes EntryCount
}
