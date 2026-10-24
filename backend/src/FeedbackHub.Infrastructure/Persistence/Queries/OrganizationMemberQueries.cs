using Microsoft.EntityFrameworkCore;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Infrastructure.Persistence.Queries;

internal sealed class OrganizationMemberQueries(AppDbContext dbContext) : IOrganizationMemberQueries
{
    public async Task<PagedResult<MemberRow>> ListActiveAsync(
        Guid organizationId, PageRequest request, CancellationToken cancellationToken)
    {
        IQueryable<OrganizationMember> active = dbContext.OrganizationMembers
            .Where(member => member.OrganizationId == organizationId && member.IsActive);

        int totalCount = await active.CountAsync(cancellationToken);

        List<MemberRow> items = await (
                from member in active
                join account in dbContext.Accounts on member.AccountId equals account.Id
                orderby EF.Functions.Collate(account.Name, Collations.IcuRoot), account.Id
                select new MemberRow(member.Id, account.Id, account.Name, account.Email))
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MemberRow>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
