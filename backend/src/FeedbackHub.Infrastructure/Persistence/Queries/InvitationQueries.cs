using Microsoft.EntityFrameworkCore;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Services.Invitations;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Infrastructure.Persistence.Queries;

internal sealed class InvitationQueries(AppDbContext dbContext) : IInvitationQueries
{
    public async Task<PagedResult<InvitationRow>> ListAsync(
        Guid organizationId, PageRequest request, CancellationToken cancellationToken)
    {
        IQueryable<OrganizationInvitation> invitations = dbContext.OrganizationInvitations
            .Where(invitation => invitation.OrganizationId == organizationId);

        int totalCount = await invitations.CountAsync(cancellationToken);

        List<InvitationRow> items = await invitations
            .OrderByDescending(invitation => invitation.ExpiresAt)
            .ThenByDescending(invitation => invitation.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(invitation => new InvitationRow(
                invitation.Id, invitation.Email, invitation.RoleId, invitation.ExpiresAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<InvitationRow>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
