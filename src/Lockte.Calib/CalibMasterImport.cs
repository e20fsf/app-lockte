using Lockte.Calib.Data;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Calib;

/// <summary>
/// The delete-and-reload side of the migration of <c>สอบเทียบ.accdb</c> (#209): registered for <c>Lockte.Migration</c> only, never in the web app. It deletes the
/// instruments and the marks of the calibration companies a run loaded; the suppliers themselves belong to purch and stay (a run marks them again). Rows are written
/// through <see cref="InstrumentRegistry"/> and <see cref="CalibrationProviderRegistry"/>, so a loaded row passes the same checks as one keyed on screen.
/// </summary>
public sealed class CalibMasterImport(CalibDbContext db)
{
    public async Task DeleteMasterAsync()
    {
        await db.InstrumentMachines.ExecuteDeleteAsync();
        await db.Instruments.ExecuteDeleteAsync();
        await db.CalibrationProviders.ExecuteDeleteAsync();
    }
}
