namespace Lockte.SharedKernel.Persistence;

/// <summary>
/// While row changes are suppressed, saves still stamp CreatedBy/UpdatedBy but write no column-by-column history:
/// for batch jobs (which record one event instead, see <see cref="HistoryEventExtensions.AddHistoryEvent"/>) and for
/// the data-migration load. Suppression follows the async call chain that asked for it, so a job running in a
/// long-lived Blazor circuit does not silence other saves of that circuit.
/// </summary>
public sealed class AuditScope
{
    private readonly AsyncLocal<int> suppressed = new();

    public bool RowChangesSuppressed => suppressed.Value > 0;

    /// <summary>Use with <c>using</c> around the job's writes, in the same async method that saves.</summary>
    public IDisposable SuppressRowChanges()
    {
        suppressed.Value++;
        return new Release(this);
    }

    private sealed class Release(AuditScope scope) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            scope.suppressed.Value--;
        }
    }
}
