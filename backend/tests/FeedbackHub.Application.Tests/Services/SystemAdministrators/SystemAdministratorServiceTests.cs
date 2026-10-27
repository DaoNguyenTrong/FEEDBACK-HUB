using NSubstitute;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.SystemAdministrators;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.SystemAdministrators;

public class SystemAdministratorServiceTests
{
    private const string Email = "boss@example.com";

    private sealed record Fixture(
        SystemAdministratorService Service,
        IUnitOfWork UnitOfWork,
        IRepository<SystemAdministrator, Guid> Administrators,
        ISystemAdministratorResolver Resolver);

    private static Account CreateAccount(bool confirmed, string email = Email)
    {
        Account account = Account.Create(new AccountParams("Boss", "boss", email));
        if (confirmed)
        {
            account.ConfirmEmail();
        }

        return account;
    }

    private static Fixture CreateFixture(Account? account = null, SystemAdministrator? existing = null)
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Account, Guid> accounts = Substitute.For<IRepository<Account, Guid>>();
        IRepository<SystemAdministrator, Guid> administrators = Substitute.For<IRepository<SystemAdministrator, Guid>>();
        unitOfWork.Repository<Account, Guid>().Returns(accounts);
        unitOfWork.Repository<SystemAdministrator, Guid>().Returns(administrators);
        RepositoryPredicateStub.StubFirstOrDefault(accounts, account is null ? [] : [account]);
        RepositoryPredicateStub.StubFirstOrDefault(administrators, existing is null ? [] : [existing]);

        ISystemAdministratorResolver resolver = Substitute.For<ISystemAdministratorResolver>();
        return new Fixture(new SystemAdministratorService(unitOfWork, resolver), unitOfWork, administrators, resolver);
    }

    private static void AssertNothingChanged(Fixture fixture)
    {
        fixture.Administrators.DidNotReceive().Update(Arg.Any<SystemAdministrator>());
        fixture.Administrators.DidNotReceive().Delete(Arg.Any<SystemAdministrator>());
        fixture.Administrators.DidNotReceive().AddAsync(Arg.Any<SystemAdministrator>(), Arg.Any<CancellationToken>());
        fixture.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        fixture.Resolver.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GrantAsync_ConfirmedAccount_AddsRowAndInvalidatesCache()
    {
        Account account = CreateAccount(confirmed: true);
        Fixture fixture = CreateFixture(account);

        SystemAdministratorChange change = await fixture.Service.GrantAsync("  Boss@Example.com ", CancellationToken.None);

        Assert.Equal(new SystemAdministratorChange(SystemAdministratorChangeOutcome.Granted, account.Id, "boss"), change);
        await fixture.Administrators.Received(1).AddAsync(
            Arg.Is<SystemAdministrator>(row => row != null && row.AccountId == account.Id), Arg.Any<CancellationToken>());
        await fixture.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await fixture.Resolver.Received(1).InvalidateAsync(account.Id, Arg.Any<CancellationToken>());
    }

    // Two grants raced and the unique index let the other one in first.
    [Fact]
    public async Task GrantAsync_SaveHitsUniqueIndex_ReturnsAlreadyGrantedWithoutInvalidating()
    {
        Account account = CreateAccount(confirmed: true);
        Fixture fixture = CreateFixture(account);
        fixture.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new ConflictException(ApplicationMessages.SystemAdministratorAlreadyGranted));

        SystemAdministratorChange change = await fixture.Service.GrantAsync(Email, CancellationToken.None);

        Assert.Equal(new SystemAdministratorChange(SystemAdministratorChangeOutcome.AlreadyGranted, account.Id, "boss"), change);
        await fixture.Resolver.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // AC-351: an unverified email cannot be trusted to belong to the person.
    [Fact]
    public async Task GrantAsync_UnconfirmedAccount_ReturnsEmailNotConfirmedAndAddsNothing()
    {
        Account account = CreateAccount(confirmed: false);
        Fixture fixture = CreateFixture(account);

        SystemAdministratorChange change = await fixture.Service.GrantAsync(Email, CancellationToken.None);

        Assert.Equal(SystemAdministratorChangeOutcome.EmailNotConfirmed, change.Outcome);
        AssertNothingChanged(fixture);
    }

    [Fact]
    public async Task GrantAsync_EmailMatchesOnlySomeonesPendingEmail_ReturnsAccountNotFound()
    {
        Account account = CreateAccount(confirmed: true, email: "other@example.com");
        account.RequestEmailChange(Email);
        Fixture fixture = CreateFixture(account);

        SystemAdministratorChange change = await fixture.Service.GrantAsync(Email, CancellationToken.None);

        Assert.Equal(new SystemAdministratorChange(SystemAdministratorChangeOutcome.AccountNotFound, null, null), change);
        AssertNothingChanged(fixture);
    }

    [Fact]
    public async Task GrantAsync_AlreadyGranted_AddsNothing()
    {
        Account account = CreateAccount(confirmed: true);
        Fixture fixture = CreateFixture(account, SystemAdministrator.Create(new SystemAdministratorParams(account.Id)));

        SystemAdministratorChange change = await fixture.Service.GrantAsync(Email, CancellationToken.None);

        Assert.Equal(SystemAdministratorChangeOutcome.AlreadyGranted, change.Outcome);
        AssertNothingChanged(fixture);
    }

    [Fact]
    public async Task RevokeAsync_Granted_RemovesRowAndInvalidatesCache()
    {
        Account account = CreateAccount(confirmed: true);
        SystemAdministrator row = SystemAdministrator.Create(new SystemAdministratorParams(account.Id));
        Fixture fixture = CreateFixture(account, row);

        SystemAdministratorChange change = await fixture.Service.RevokeAsync(Email, CancellationToken.None);

        Assert.Equal(new SystemAdministratorChange(SystemAdministratorChangeOutcome.Revoked, account.Id, "boss"), change);
        fixture.Administrators.Received(1).Delete(row);
        await fixture.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await fixture.Resolver.Received(1).InvalidateAsync(account.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_NotGranted_ChangesNothing()
    {
        Account account = CreateAccount(confirmed: true);
        Fixture fixture = CreateFixture(account);

        SystemAdministratorChange change = await fixture.Service.RevokeAsync(Email, CancellationToken.None);

        Assert.Equal(new SystemAdministratorChange(SystemAdministratorChangeOutcome.NotGranted, account.Id, "boss"), change);
        AssertNothingChanged(fixture);
    }

    [Fact]
    public async Task RevokeAsync_UnknownEmail_ReturnsAccountNotFound()
    {
        Fixture fixture = CreateFixture();

        SystemAdministratorChange change = await fixture.Service.RevokeAsync(Email, CancellationToken.None);

        Assert.Equal(SystemAdministratorChangeOutcome.AccountNotFound, change.Outcome);
        AssertNothingChanged(fixture);
    }

    // Revoking does not depend on the email being verified.
    [Fact]
    public async Task RevokeAsync_UnconfirmedGrantedAccount_StillRevokes()
    {
        Account account = CreateAccount(confirmed: false);
        Fixture fixture = CreateFixture(account, SystemAdministrator.Create(new SystemAdministratorParams(account.Id)));

        SystemAdministratorChange change = await fixture.Service.RevokeAsync(Email, CancellationToken.None);

        Assert.Equal(SystemAdministratorChangeOutcome.Revoked, change.Outcome);
    }
}
