using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Infrastructure.Persistence;

public sealed class UnitOfWork(
    AppDbContext dbContext,
    IServiceProvider serviceProvider) : IUnitOfWork
{
    private readonly Dictionary<(Type, Type), object> repositories = [];

    public IRepository<T, TId> Repository<T, TId>()
        where T : BaseEntity<TId>
        where TId : notnull
    {
        (Type, Type) key = (typeof(T), typeof(TId));

        if (!repositories.TryGetValue(key, out object? repository))
        {
            repository = serviceProvider.GetRequiredService<IRepository<T, TId>>();
            repositories[key] = repository;
        }

        return (IRepository<T, TId>)repository;
    }

    public IRepository<T> Repository<T>()
        where T : BaseEntity
    {
        (Type, Type) key = (typeof(T), typeof(int));

        if (!repositories.TryGetValue(key, out object? repository))
        {
            repository = serviceProvider.GetRequiredService<IRepository<T>>();
            repositories[key] = repository;
        }

        return (IRepository<T>)repository;
    }

    /// <summary>Name of the unique index on <c>accounts.Email</c> (see the InitialCreate migration).</summary>
    internal const string AccountEmailUniqueIndexName = "IX_accounts_Email";

    /// <summary>Name of the unique index on <c>system_administrators.AccountId</c> (AddSystemAdministrators migration).</summary>
    internal const string SystemAdministratorAccountUniqueIndexName = "IX_system_administrators_AccountId";

    /// <summary>Name of the unique index on <c>organizations.Slug</c> (AddOrganizations migration).</summary>
    internal const string OrganizationSlugUniqueIndexName = "IX_organizations_Slug";

    /// <summary>Name of the unique index on <c>roles (OrganizationId, lower(Name))</c> (RenameDuplicateRoleNames migration).</summary>
    internal const string RoleNameUniqueIndexName = "IX_roles_OrganizationId_lower_Name";

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: AccountEmailUniqueIndexName
        })
        {
            // Two writers claimed the same address between the service's duplicate check and the save.
            throw new ConflictException(ApplicationMessages.AccountEmailAlreadyExists);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: SystemAdministratorAccountUniqueIndexName
        })
        {
            // Two grants for the same account raced past the service's existence check.
            throw new ConflictException(ApplicationMessages.SystemAdministratorAlreadyGranted);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: OrganizationSlugUniqueIndexName
        })
        {
            // Two creations raced past the service's slug check; the loser must not learn who won (AC-330).
            throw new ConflictException(ApplicationMessages.OrganizationSlugAlreadyExists);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: RoleNameUniqueIndexName
        })
        {
            // Two role writes raced past the service's name check (AC-290, AC-326).
            throw new ConflictException(ApplicationMessages.OrganizationRoleNameAlreadyExists);
        }
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        // Postgres can't nest transactions — join the open one and leave commit/rollback to its owner.
        if (dbContext.Database.CurrentTransaction is not null)
        {
            await operation(cancellationToken);
            return;
        }

        // Disposing without a commit (operation threw) rolls back.
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await operation(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
