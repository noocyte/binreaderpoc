namespace BinReader.Models;

public readonly record struct FieldChange(
    DateTime Timestamp,
    FieldType FieldType,
    double RawValue)
{
    public static FieldChange ForNumber(DateTime ts, double value)
        => new(ts, FieldType.Number, value);

    public static FieldChange ForDateTime(DateTime ts, DateTime value)
        => new(ts, FieldType.DateTime, BitConverter.Int64BitsToDouble(value.Ticks));

    public static FieldChange ForBool(DateTime ts, bool value)
        => new(ts, FieldType.Bool, value ? 1.0 : 0.0);

    public double AsNumber() => RawValue;

    public DateTime AsDateTime() => new(BitConverter.DoubleToInt64Bits(RawValue));

    public bool AsBool() => RawValue != 0.0;
}
