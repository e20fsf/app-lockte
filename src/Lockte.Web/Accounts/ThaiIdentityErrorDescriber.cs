using Lockte.SharedKernel.Persistence;
using Microsoft.AspNetCore.Identity;

namespace Lockte.Web.Accounts;

/// <summary>Thai messages for the Identity errors this system can hit (no email, no roles, simple password rule).</summary>
public sealed class ThaiIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError()
        => new() { Code = nameof(DefaultError), Description = "บันทึกไม่สำเร็จ กรุณาลองใหม่" };

    public override IdentityError ConcurrencyFailure()
        => new() { Code = nameof(ConcurrencyFailure), Description = ConcurrencyConflictException.UserMessage };

    public override IdentityError PasswordMismatch()
        => new() { Code = nameof(PasswordMismatch), Description = "รหัสผ่านเดิมไม่ถูกต้อง" };

    public override IdentityError InvalidUserName(string? userName)
        => new() { Code = nameof(InvalidUserName), Description = $"ชื่อ user '{userName}' ใช้ได้เฉพาะตัวอักษรอังกฤษ ตัวเลข และ - . _" };

    public override IdentityError DuplicateUserName(string userName)
        => new() { Code = nameof(DuplicateUserName), Description = $"มีชื่อ user '{userName}' อยู่แล้ว" };

    public override IdentityError PasswordTooShort(int length)
        => new() { Code = nameof(PasswordTooShort), Description = $"รหัสผ่านต้องมีอย่างน้อย {length} ตัว" };
}
