namespace Lockte.SharedKernel.Persistence;

/// <summary>
/// Per request / circuit / job. While row changes are suppressed, saves still stamp CreatedBy/UpdatedBy but
/// write no column-by-column history: for batch jobs (which record one event instead, see
/// <see cref="HistoryEventExtensions.AddHistoryEvent"/>) and for the data-migration load.
/// </summary>
public sealed class AuditScope
{
    private int suppressed;

    public bool RowChangesSuppressed => suppressed > 0;

    public IDisposable SuppressRowChanges()
    {
        suppressed++;
        return new Release(this);
    }

    private sealed class Release(AuditScope scope) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            scope.suppressed--;
        }
    }
}
