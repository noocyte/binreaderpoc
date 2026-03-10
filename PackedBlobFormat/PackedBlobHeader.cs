using BinReader.Models;

namespace BinReader.PackedBlobFormat;

public readonly record struct PackedBlobHeader(FieldType FieldType, byte ValueSize, int ArticleCount)
{
    public const int Size = 6; // 1 byte FieldType + 1 byte ValueSize + 4 bytes ArticleCount

    public static byte GetValueSize(FieldType fieldType) => fieldType == FieldType.Bool ? (byte)1 : (byte)8;
}
