namespace FeedbackHub.Application.Common.Interfaces;

public interface ICurrentTenantProvider
{
    Guid? OrganizationId { get; }
}
