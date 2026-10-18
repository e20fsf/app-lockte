using Microsoft.AspNetCore.Identity;

namespace Lockte.Web.Accounts;

public enum LoginStatus
{
    Succeeded,
    /// <summary>Password correct, but it is a temporary one: sign in only to the change-password page.</summary>
    MustChangePassword,
    LockedOut,
    Disabled,
    Invalid,
}

public sealed record LoginResult(LoginStatus Status, string? UserId = null, DateTimeOffset? LockedUntil = null)
{
    public bool CanSignIn => Status is LoginStatus.Succeeded or LoginStatus.MustChangePassword;
}

/// <summary>Outcome of an account change; <see cref="Errors"/> are Thai messages for the screen.</summary>
public sealed record AccountResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static AccountResult Ok { get; } = new(true, []);

    public static AccountResult Fail(params string[] errors) => new(false, errors);

    public static AccountResult From(IdentityResult result)
        => result.Succeeded ? Ok : new(false, result.Errors.Select(e => e.Description).ToArray());
}

public sealed record UserSummary(
    string Id,
    string UserName,
    string DisplayName,
    bool IsAdmin,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset? LockedUntil,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string UpdatedBy,
    DateTimeOffset UpdatedAt,
    byte[] RowVersion);
