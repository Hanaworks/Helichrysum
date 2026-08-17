namespace Helichrysum.Core.Tests;

/// <summary>
/// Shared helpers for tests that need to be resilient to platform differences
/// (particularly Windows file-locking delays after disposing connections).
/// </summary>
public static class TestFileHelper
{
    /// <summary>
    /// Deletes a file with retries, tolerating transient Windows file-lock delays.
    /// </summary>
    public static void DeleteFileWithRetry(string path, int attempts = 10)
    {
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < attempts)
            {
                Thread.Sleep(100 * attempt);
            }
        }
    }

    /// <summary>
    /// Deletes a directory recursively with retries, tolerating transient locks.
    /// </summary>
    public static void DeleteDirectoryWithRetry(string path, int attempts = 10)
    {
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < attempts)
            {
                Thread.Sleep(100 * attempt);
            }
        }
    }
}