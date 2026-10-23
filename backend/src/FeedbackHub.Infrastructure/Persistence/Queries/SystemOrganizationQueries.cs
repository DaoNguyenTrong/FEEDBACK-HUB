using Microsoft.EntityFrameworkCore;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Services.SystemOrganizations;

namespace FeedbackHub.Infrastructure.Persistence.Queries;

internal sealed class SystemOrganizationQueries(AppDbContext dbContext) : ISystemOrganizationQueries
{
    // ICU root collation: "an Bình" < "Âu Cơ" < "Bảo Minh", where the database default would sort by code point.
    private const string NameCollation = "und-x-icu";

    public async Task<PagedResult<SystemOrganizationDto>> ListAsync(
        PageRequest request, CancellationToken cancellationToken)
    {
        int totalCount = await dbContext.Organizations.CountAsync(cancellationToken);

        List<SystemOrganizationDto> items = await dbContext.Organizations
            .OrderBy(organization => EF.Functions.Collate(organization.Name, NameCollation))
            .ThenBy(organization => organization.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(organization => new SystemOrganizationDto(
                organization.Id,
                organization.Name,
                dbContext.OrganizationMembers.Count(member =>
                    member.OrganizationId == organization.Id && member.IsActive),
                !organization.Status))
            .ToListAsync(cancellationToken);

        return new PagedResult<SystemOrganizationDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
