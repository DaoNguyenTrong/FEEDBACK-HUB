using System.Text.Json;
using Microsoft.Extensions.Logging;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Application.Services.AuditLogs;

public sealed class AuditLogService(
    IAuditLogRepository auditLogRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentUserService currentUserService,
    IRoleGrantReader roleGrantReader,
    ILogger<AuditLogService> logger) : IAuditLogService
{
    public async Task<AuditEntryPage> ListAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.Limit, 100);
        Guid organizationId = RequireOrganizationId();
        Guid accountId = GetCurrentAccountId();

        // The database, not the permission cache, decides who reads member and invited emails (BR-75).
        RoleGrantCaller caller = await roleGrantReader.LoadCallerAsync(organizationId, accountId, cancellationToken);
        if (!caller.Permissions.Contains(Permissions.AuditLogsView))
        {
            throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);
        }

        AuditCursor? before = null;
        if (query.Cursor is not null)
        {
            before = AuditCursor.TryDecode(query.Cursor, out AuditCursor decoded)
                ? decoded
                : throw new DomainException(ApplicationMessages.InvalidCursor);
        }

        IReadOnlyList<AuditEventRow> rows = await auditLogRepository.ListEventsAsync(
            organizationId, before, query.Limit + 1, cancellationToken);

        // Taken from the raw rows, so skipping an unmappable row never ends the paging early.
        string? nextCursor = rows.Count > query.Limit
            ? new AuditCursor(rows[query.Limit - 1].Timestamp, rows[query.Limit - 1].Id).Encode()
            : null;

        List<AuditEntryDto> items = [];
        foreach (AuditEventRow row in rows.Take(query.Limit))
        {
            if (TryBuildEntry(row, out AuditEntryDto? entry))
            {
                items.Add(entry);
            }
            else
            {
                logger.LogWarning("Skipped audit entry {AuditLogId}", row.Id);
            }
        }

        return new AuditEntryPage(items, nextCursor);
    }

    private static bool TryBuildEntry(AuditEventRow row, out AuditEntryDto entry)
    {
        entry = default!;
        if (!TryParseActorKind(row.ActorKind, out ActorKind actorKind)
            || !AuditEventCatalog.TryResolve(row.EntityName, row.Action, out AuditEventType type)
            || !TryReadParameters(row.NewValues, AuditEventCatalog.Get(type), out Dictionary<string, object> parameters))
        {
            return false;
        }

        // A system administrator acts across organizations, so the log never names them (AC-368).
        string? actorName = actorKind == ActorKind.Account ? row.ActorName : null;
        entry = new AuditEntryDto(row.Id, row.Timestamp, actorKind, actorName, type, parameters);
        return true;
    }

    // Enum.TryParse alone would accept "1" and " Account"; the name must match exactly or the row is skipped.
    private static bool TryParseActorKind(string? raw, out ActorKind kind)
    {
        if (raw is not null
            && Enum.TryParse(raw, ignoreCase: false, out kind)
            && Enum.IsDefined(kind)
            && string.Equals(kind.ToString(), raw, StringComparison.Ordinal))
        {
            return true;
        }

        kind = default;
        return false;
    }

    private static bool TryReadParameters(
        string? newValues, AuditEventDefinition definition, out Dictionary<string, object> parameters)
    {
        parameters = [];
        if (string.IsNullOrWhiteSpace(newValues))
        {
            // AuditTrail stores NULL when an event has no parameters.
            return definition.Parameters.Count == 0;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(newValues);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach ((string name, AuditParameterKind kind) in definition.Parameters)
            {
                if (!document.RootElement.TryGetProperty(name, out JsonElement value))
                {
                    continue;
                }

                if (!AuditParameterReader.TryRead(value, kind, out object read))
                {
                    return false;
                }

                parameters[name] = read;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private Guid GetCurrentAccountId() =>
        Guid.TryParse(currentUserService.UserId, out Guid accountId)
            ? accountId
            : throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);

    private Guid RequireOrganizationId() =>
        currentTenantProvider.OrganizationId
            ?? throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);
}
