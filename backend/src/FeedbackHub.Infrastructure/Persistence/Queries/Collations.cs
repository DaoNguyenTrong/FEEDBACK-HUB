namespace FeedbackHub.Infrastructure.Persistence.Queries;

internal static class Collations
{
    // ICU root collation: "an Bình" < "Âu Cơ" < "Bảo Minh", where the database default would sort by code point.
    public const string IcuRoot = "und-x-icu";
}
