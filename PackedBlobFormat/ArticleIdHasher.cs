using System.IO.Hashing;

namespace BinReader.PackedBlobFormat;

public static class ArticleIdHasher
{
    public static ulong Hash(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);
        return XxHash3.HashToUInt64(bytes);
    }
}
