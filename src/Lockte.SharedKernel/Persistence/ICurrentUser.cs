namespace Lockte.SharedKernel.Persistence;

/// <summary>Who is making the change — the user name written to CreatedBy/UpdatedBy.</summary>
public interface ICurrentUser
{
    string UserName { get; }
}

/// <summary>Changes made outside a signed-in user, e.g. the reset-admin command on the server.</summary>
public sealed class SystemUser : ICurrentUser
{
    public const string Name = "system";
    public string UserName => Name;
}
