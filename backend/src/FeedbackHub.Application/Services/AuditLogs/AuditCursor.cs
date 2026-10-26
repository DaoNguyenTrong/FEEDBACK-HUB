using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace FeedbackHub.Application.Services.AuditLogs;

/// <summary>Keyset position in the audit log, ordered by timestamp then id, both descending.</summary>
public readonly record struct AuditCursor(DateTime TimestampUtc, long Id)
{
    // A valid cursor is at most 52 characters; the cap bounds the work done on hostile input.
    private const int MaxLength = 64;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public string Encode()
    {
        string payload = string.Create(CultureInfo.InvariantCulture, $"{TimestampUtc.Ticks}:{Id}");
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string value, out AuditCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength || value.Contains('='))
        {
            return false;
        }

        string payload;
        try
        {
            payload = StrictUtf8.GetString(Base64Url.DecodeFromChars(value));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return false;
        }

        string[] parts = payload.Split(':');
        if (parts.Length != 2
            || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks)
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long id)
            || ticks > DateTime.MaxValue.Ticks
            || id <= 0)
        {
            return false;
        }

        cursor = new AuditCursor(new DateTime(ticks, DateTimeKind.Utc), id);
        return true;
    }
}
