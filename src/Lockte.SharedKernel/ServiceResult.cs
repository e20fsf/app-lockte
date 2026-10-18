namespace Lockte.SharedKernel;

/// <summary>
/// Outcome of a module service's save. <see cref="Errors"/> are Thai messages shown to the user as is;
/// a concurrency conflict is thrown instead (<see cref="Persistence.ConcurrencyConflictException"/>).
/// </summary>
public sealed record ServiceResult(bool Succeeded, IReadOnlyList<string> Errors, int? Id = null)
{
    public static ServiceResult Ok(int? id = null) => new(true, [], id);

    public static ServiceResult Fail(params string[] errors) => new(false, errors);
}
