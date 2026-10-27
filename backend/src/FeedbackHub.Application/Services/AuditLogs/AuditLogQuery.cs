using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.AuditLogs;

/// <param name="Cursor">The <c>nextCursor</c> of the previous page; leave out for the newest entries.</param>
/// <param name="Limit">Entries per page, 1 to 100.</param>
public sealed record AuditLogQuery(
    string? Cursor = null,
    [Range(1, 100, ErrorMessage = "FieldRange")] int Limit = 50);
