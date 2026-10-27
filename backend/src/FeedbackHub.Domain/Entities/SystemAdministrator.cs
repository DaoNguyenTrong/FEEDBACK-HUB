using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Entities;

public record SystemAdministratorParams(Guid AccountId);

/// <summary>
/// An account allowed into the system-administrator area. Belongs to no organization (TECH00 §3.5); only the
/// server's CLI commands write these rows.
/// </summary>
public sealed class SystemAdministrator : BaseEntity<Guid>
{
    private SystemAdministrator()
    {
    }

    public static SystemAdministrator Create(SystemAdministratorParams p)
    {
        SystemAdministrator administrator = new()
        {
            Id = IdGenerator.NewUuidV7()
        };
        administrator.Update(p);
        return administrator;
    }

    public Guid AccountId { get; private set; }

    public void Update(SystemAdministratorParams p)
    {
        if (p.AccountId == Guid.Empty)
        {
            throw new DomainException(DomainMessages.AccountIdRequired);
        }

        AccountId = p.AccountId;
    }
}
