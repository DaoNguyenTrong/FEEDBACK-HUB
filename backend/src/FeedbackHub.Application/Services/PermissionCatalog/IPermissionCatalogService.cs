namespace FeedbackHub.Application.Services.PermissionCatalog;

public interface IPermissionCatalogService
{
    IReadOnlyList<PermissionDto> List();
}
