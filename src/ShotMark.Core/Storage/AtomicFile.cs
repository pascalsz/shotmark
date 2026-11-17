namespace ShotMark.Core.Storage;

public static class AtomicFile
{
    public static void Write(string path, byte[] content, int attempts = 4, Action<TimeSpan>? sleep = null)
    {
        sleep ??= Thread.Sleep;
        for (var attempt = 1; ; attempt++)
        {
            var temp = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(content);
                    // Erst auf die Platte, dann umbenennen – sonst kann nach Stromausfall eine leere Datei synchronisiert werden
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temp, path, overwrite: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                TryDelete(temp);
                if (attempt >= attempts)
                {
                    if (e is IOException) throw;
                    throw new IOException(e.Message, e);
                }
                sleep(TimeSpan.FromMilliseconds(150 * attempt * attempt));
            }
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
