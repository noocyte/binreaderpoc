using BinReader.Models;

namespace BinReader.DataGeneration;

public static class DataGenerator
{
    private static readonly (string Name, FieldType Type)[] FieldPool =
    [
        ("price",        FieldType.Number),
        ("rating",       FieldType.Number),
        ("weight",       FieldType.Number),
        ("publishDate",  FieldType.DateTime),
        ("lastReviewed", FieldType.DateTime),
        ("expiryDate",   FieldType.DateTime),
        ("isActive",     FieldType.Bool),
        ("isFeatured",   FieldType.Bool),
        ("isArchived",   FieldType.Bool),
    ];

    public static List<Article> GenerateArticles(int count = 1000, int seed = 42)
    {
        var rng = new Random(seed);
        var articles = new List<Article>(count);

        for (var i = 0; i < count; i++)
        {
            var fieldCount = rng.Next(2, 5); // 2-4 fields
            var chosenFields = PickRandomSubset(FieldPool, fieldCount, rng);
            var fields = new Dictionary<string, List<FieldChange>>();

            foreach (var (name, fieldType) in chosenFields)
            {
                var changeCount = rng.Next(5, 21); // 5-20 changes
                fields[name] = GenerateChanges(fieldType, changeCount, rng);
            }

            articles.Add(new Article
            {
                Id = Guid.NewGuid(),
                Fields = fields
            });
        }

        return articles;
    }

    public static List<Article> GenerateSingleFieldArticles(
        string fieldName, FieldType fieldType,
        int articleCount = 2000, int minChanges = 100, int maxChanges = 2000, int seed = 42)
    {
        var rng = new Random(seed);
        var articles = new List<Article>(articleCount);

        for (var i = 0; i < articleCount; i++)
        {
            var changeCount = rng.Next(minChanges, maxChanges + 1);
            articles.Add(new Article
            {
                Id = Guid.NewGuid(),
                Fields = new Dictionary<string, List<FieldChange>>
                {
                    [fieldName] = GenerateChanges(fieldType, changeCount, rng)
                }
            });
        }

        return articles;
    }

    private static T[] PickRandomSubset<T>(T[] pool, int count, Random rng)
    {
        var copy = pool.ToArray();
        for (var i = 0; i < count; i++)
        {
            var j = rng.Next(i, copy.Length);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy[..count];
    }

    private static List<FieldChange> GenerateChanges(FieldType type, int count, Random rng)
    {
        var baseDate = new DateTime(2020, 1, 1).AddDays(rng.Next(0, 1095));
        var changes = new List<FieldChange>(count);
        var current = baseDate;

        for (var i = 0; i < count; i++)
        {
            current = current
                .AddDays(rng.Next(1, 91))
                .AddHours(rng.Next(0, 24))
                .AddMinutes(rng.Next(0, 60));

            var change = type switch
            {
                FieldType.Number => FieldChange.ForNumber(current,
                    Math.Round(rng.NextDouble() * 1000, 2)),
                FieldType.DateTime => FieldChange.ForDateTime(current,
                    current.AddDays(rng.Next(-30, 365))),
                FieldType.Bool => FieldChange.ForBool(current,
                    rng.Next(2) == 1),
                _ => throw new ArgumentOutOfRangeException()
            };

            changes.Add(change);
        }

        return changes;
    }
}
