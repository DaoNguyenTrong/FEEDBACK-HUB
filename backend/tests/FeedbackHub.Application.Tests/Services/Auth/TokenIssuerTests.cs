using Microsoft.Extensions.Options;
using NSubstitute;
using FeedbackHub.Application.Common;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Auth;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Auth;

public class TokenIssuerTests
{
    [Fact]
    public async Task IssueTokensAsync_LockedAccount_Throws401AndCreatesNoRefreshToken()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<RefreshToken, long> refreshTokenRepo = Substitute.For<IRepository<RefreshToken, long>>();
        unitOfWork.Repository<RefreshToken, long>().Returns(refreshTokenRepo);
        TokenIssuer issuer = new(
            unitOfWork,
            Substitute.For<IJwtTokenService>(),
            Substitute.For<IPermissionResolver>(),
            Options.Create(new JwtSettings()),
            Options.Create(new StorageSettings()));
        Account account = Account.Create(new AccountParams("Locked", "locked-user", "locked@example.com"));
        account.Deactivate();

        UnauthorizedException exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            issuer.IssueTokensAsync(
                account, null, null, null, false, DateTime.UtcNow, familyId: null, CancellationToken.None));

        Assert.Equal(ApplicationMessages.InvalidUsernameOrPassword, exception.Message);
        await refreshTokenRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }
}
