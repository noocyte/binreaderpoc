namespace BinReader.Query;

public enum TimeFilterMode
{
    Before,
    After,
    Between
}

public record TimeFilter(TimeFilterMode Mode, DateTime From, DateTime? To = null)
{
    public static TimeFilter BeforeTime(DateTime time) => new(TimeFilterMode.Before, time);
    public static TimeFilter AfterTime(DateTime time) => new(TimeFilterMode.After, time);
    public static TimeFilter BetweenTimes(DateTime from, DateTime to) => new(TimeFilterMode.Between, from, to);
}
