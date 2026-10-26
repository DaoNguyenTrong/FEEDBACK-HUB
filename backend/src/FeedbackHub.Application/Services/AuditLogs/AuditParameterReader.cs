using System.Text.Json;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.AuditLogs;

// Strict on purpose: stored JSON is untrusted text, so a wrong shape is rejected, never coerced.
internal static class AuditParameterReader
{
    public static bool TryRead(JsonElement value, AuditParameterKind kind, out object result)
    {
        result = default!;
        switch (kind)
        {
            case AuditParameterKind.Text when value.ValueKind == JsonValueKind.String:
                result = value.GetString()!;
                return true;
            case AuditParameterKind.TextList when TryReadList(value, TryReadText, out string[] texts):
                result = texts;
                return true;
            case AuditParameterKind.Role when TryReadRole(value, out RoleRefDto? role):
                result = role;
                return true;
            case AuditParameterKind.RoleList when TryReadList(value, TryReadRole, out RoleRefDto[] roles):
                result = roles;
                return true;
            default:
                return false;
        }
    }

    private delegate bool ItemReader<T>(JsonElement element, out T item);

    private static bool TryReadText(JsonElement element, out string text)
    {
        text = element.ValueKind == JsonValueKind.String ? element.GetString()! : string.Empty;
        return element.ValueKind == JsonValueKind.String;
    }

    private static bool TryReadList<T>(JsonElement value, ItemReader<T> readItem, out T[] items)
    {
        items = [];
        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        List<T> read = [];
        foreach (JsonElement element in value.EnumerateArray())
        {
            if (!readItem(element, out T item))
            {
                return false;
            }

            read.Add(item);
        }

        items = [.. read];
        return true;
    }

    private static bool TryReadRole(JsonElement value, out RoleRefDto role)
    {
        role = default!;
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("id", out JsonElement id)
            || id.ValueKind != JsonValueKind.String
            || !Guid.TryParse(id.GetString(), out Guid roleId)
            || roleId == Guid.Empty
            || !value.TryGetProperty("name", out JsonElement name)
            || name.ValueKind != JsonValueKind.String
            || !TryReadSystemKind(value, out SystemRoleKind? systemKind))
        {
            return false;
        }

        role = new RoleRefDto(roleId, name.GetString()!, systemKind);
        return true;
    }

    private static bool TryReadSystemKind(JsonElement role, out SystemRoleKind? kind)
    {
        kind = null;
        if (!role.TryGetProperty("systemKind", out JsonElement raw) || raw.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        string? text = raw.ValueKind == JsonValueKind.String ? raw.GetString() : null;
        if (!TryParseExactName(text, out SystemRoleKind parsed))
        {
            return false;
        }

        kind = parsed;
        return true;
    }

    // Enum.TryParse alone would accept "1" and " Account"; the name must match exactly.
    internal static bool TryParseExactName<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        if (value is not null
            && Enum.TryParse(value, ignoreCase: false, out result)
            && Enum.IsDefined(result)
            && string.Equals(result.ToString(), value, StringComparison.Ordinal))
        {
            return true;
        }

        result = default;
        return false;
    }
}
