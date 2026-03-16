namespace BinReader.PackedBlobFormat;

public static class ShardKey
{
    public static char ForGuid(Guid id) => id.ToString("N")[0];
}
