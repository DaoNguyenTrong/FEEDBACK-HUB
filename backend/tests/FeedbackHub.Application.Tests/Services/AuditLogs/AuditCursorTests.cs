using System.Buffers.Text;
using System.Text;
using FeedbackHub.Application.Services.AuditLogs;

namespace FeedbackHub.Application.Tests.Services.AuditLogs;

public class AuditCursorTests
{
    private static string EncodePayload(string payload) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload));

    [Fact]
    public void Encode_ThenTryDecode_RoundTripsUtcTimestampAndId()
    {
        DateTime timestamp = new(638_000_000_000_000_123, DateTimeKind.Utc);
        AuditCursor cursor = new(timestamp, 42);

        bool decoded = AuditCursor.TryDecode(cursor.Encode(), out AuditCursor result);

        Assert.True(decoded);
        Assert.Equal(timestamp, result.TimestampUtc);
        Assert.Equal(DateTimeKind.Utc, result.TimestampUtc.Kind);
        Assert.Equal(42, result.Id);
    }

    [Fact]
    public void Encode_IsBase64UrlWithoutPadding()
    {
        // Ticks and id are chosen so the standard base64 form would carry '+', '/' or '='.
        string encoded = new AuditCursor(new DateTime(DateTime.MaxValue.Ticks, DateTimeKind.Utc), long.MaxValue).Encode();

        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
        Assert.DoesNotContain('=', encoded);
        Assert.True(encoded.Length <= 64);
    }

    [Fact]
    public void TryDecode_MaxTicksAndZeroTicks_AreAccepted()
    {
        Assert.True(AuditCursor.TryDecode(EncodePayload($"{DateTime.MaxValue.Ticks}:1"), out AuditCursor max));
        Assert.Equal(DateTime.MaxValue.Ticks, max.TimestampUtc.Ticks);
        Assert.True(AuditCursor.TryDecode(EncodePayload("0:1"), out AuditCursor min));
        Assert.Equal(0, min.TimestampUtc.Ticks);
    }

    [Theory]
    [InlineData("@@@", false)]
    [InlineData("", false)]
    [InlineData("A=", false)]
    [InlineData("MTIz", false)]
    [InlineData("abc:5", true)]
    [InlineData("-1:5", true)]
    [InlineData("3155378976000000000:5", true)]
    [InlineData("638000000000000000:0", true)]
    [InlineData("638000000000000000:-3", true)]
    [InlineData("638000000000000000:x", true)]
    [InlineData("638000000000000000:5:6", true)]
    [InlineData(" 638000000000000000:5", true)]
    [InlineData("+638000000000000000:5", true)]
    [InlineData("638000000000000000:", true)]
    [InlineData(":5", true)]
    public void TryDecode_Malformed_ReturnsFalse(string value, bool isPayload)
    {
        string input = isPayload ? EncodePayload(value) : value;

        bool decoded = AuditCursor.TryDecode(input, out _);

        Assert.False(decoded);
    }

    [Fact]
    public void TryDecode_Null_ReturnsFalse()
    {
        Assert.False(AuditCursor.TryDecode(null!, out _));
    }

    [Fact]
    public void TryDecode_LongerThan64Characters_ReturnsFalse()
    {
        string tooLong = new('A', 100);

        Assert.False(AuditCursor.TryDecode(tooLong, out _));
    }

    [Fact]
    public void TryDecode_PaddedValidPayload_ReturnsFalse()
    {
        string padded = EncodePayload("638000000000000000:5") + "=";

        Assert.False(AuditCursor.TryDecode(padded, out _));
    }

    [Fact]
    public void TryDecode_InvalidUtf8Bytes_ReturnsFalse()
    {
        string invalid = Base64Url.EncodeToString([0xFF, 0xFE, 0x3A, 0x35]);

        Assert.False(AuditCursor.TryDecode(invalid, out _));
    }
}
