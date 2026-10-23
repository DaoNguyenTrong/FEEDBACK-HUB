namespace FeedbackHub.Application.Services.Organizations;

public interface IOrganizationService
{
    /// <exception cref="Domain.Exceptions.ForbiddenException">The caller's email is not confirmed.</exception>
    /// <exception cref="Domain.Exceptions.DomainException">The slug is invalid.</exception>
    /// <exception cref="Domain.Exceptions.ConflictException">Another organization, of any status, has the slug.</exception>
    Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken);

    /// <summary>Every organization the caller is an active member of, deactivated ones included.</summary>
    Task<IReadOnlyList<OrganizationDto>> ListMineAsync(CancellationToken cancellationToken);
}
