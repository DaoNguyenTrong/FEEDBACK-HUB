using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Tests.Entities;

public class SystemAdministratorTests
{
    [Fact]
    public void Create_WithAccountId_AssignsAccountAndGeneratesUuidV7()
    {
        Guid accountId = Guid.NewGuid();

        SystemAdministrator administrator = SystemAdministrator.Create(new SystemAdministratorParams(accountId));

        Assert.NotEqual(Guid.Empty, administrator.Id);
        Assert.Equal(7, administrator.Id.Version);
        Assert.Equal(accountId, administrator.AccountId);
    }

    [Fact]
    public void Create_EmptyAccountId_Throws()
    {
        DomainAssert.ThrowsWithMessage(
            DomainMessages.AccountIdRequired,
            () => SystemAdministrator.Create(new SystemAdministratorParams(Guid.Empty)));
    }

    [Fact]
    public void Update_EmptyAccountId_ThrowsAndKeepsAccount()
    {
        Guid accountId = Guid.NewGuid();
        SystemAdministrator administrator = SystemAdministrator.Create(new SystemAdministratorParams(accountId));

        DomainAssert.ThrowsWithMessage(
            DomainMessages.AccountIdRequired,
            () => administrator.Update(new SystemAdministratorParams(Guid.Empty)));
        Assert.Equal(accountId, administrator.AccountId);
    }
}
