namespace iTunesRPC;

// Writes to both the console (useful when run from a terminal) and a log file next
// to the exe (the only way to see anything once the app is running windowless via
// autorun) - needed to diagnose issues like the intermittent album art disappearing
// on long sessions, where nobody's watching a console.
public static class Log
{
    private const long MaxSizeBytes = 2 * 1024 * 1024;
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "itunes-rpc.log");

    public static void Write(string message)
    {
        Console.WriteLine(message);

        try
        {
            var file = new FileInfo(FilePath);
            if (file.Exists && file.Length > MaxSizeBytes)
            {
                File.Delete(FilePath);
            }

            File.AppendAllText(FilePath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging is best-effort - never let a log write failure take down the app.
        }
    }
}
