namespace BinReader.Models;

public class Article
{
    public required Guid Id { get; init; }
    public required Dictionary<string, List<FieldChange>> Fields { get; init; }
}
