using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Accounts;

/// <summary>
/// The program (โปรแกรมพนักงาน / ผลผลิตและORDER) the user last worked in, so the next login opens it straight away.
/// A preference, not business data: not <see cref="Lockte.SharedKernel.Persistence.IAudited"/>, so switching writes no history.
/// </summary>
public sealed class UserLastProgram
{
    public const int ProgramLength = 20;

    public string UserId { get; set; } = "";
    /// <summary>The program's key (<see cref="Menus.ProgramMenu.Key"/>).</summary>
    public string Program { get; set; } = "";
}

public sealed class LastProgramService(AccountsDbContext db)
{
    /// <summary>Null when the user has not opened any program yet.</summary>
    public async Task<string?> GetAsync(string userId)
        => await db.LastPrograms.AsNoTracking().Where(p => p.UserId == userId).Select(p => p.Program).FirstOrDefaultAsync();

    public async Task SetAsync(string userId, string program)
    {
        var row = await db.LastPrograms.FirstOrDefaultAsync(p => p.UserId == userId);
        if (row is null) db.LastPrograms.Add(new UserLastProgram { UserId = userId, Program = program });
        else if (row.Program == program) return;
        else row.Program = program;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (row is null)
        {
            // The same user in another tab saved first; either choice is a fine "last" one.
        }
    }
}
