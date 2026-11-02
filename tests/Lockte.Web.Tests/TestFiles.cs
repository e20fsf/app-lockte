using System.Data.OleDb;

namespace Lockte.Web.Tests;

public static class TestFiles
{
    /// <summary>
    /// Deletes a scratch folder holding Access files. Access lets go of its lock file a moment after the last connection closes, longer when the
    /// machine is busy with the rest of the run (#381); the folder is only scratch space, so wait for the lock and never fail a finished test
    /// over a leftover temp folder.
    /// </summary>
    public static void DeleteScratch(DirectoryInfo folder)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (OperatingSystem.IsWindows()) OleDbConnection.ReleaseObjectPool();
            try { folder.Delete(recursive: true); return; }
            catch (DirectoryNotFoundException) { return; }
            catch (IOException) { Thread.Sleep(250); }
            catch (UnauthorizedAccessException) { Thread.Sleep(250); }
        }
    }
}
