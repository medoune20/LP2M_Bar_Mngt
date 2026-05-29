using System.IO;

namespace LP2M_Bar_Mngt.Presentation.Diagnostics;

internal static class StartupLogger
{
    private static readonly object SyncRoot = new();

    public static string LogFilePath
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LP2M_Bar_Mngt");

            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "startup.log");
        }
    }

    public static void Write(string message)
    {
        lock (SyncRoot)
        {
            File.AppendAllText(LogFilePath, $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
    }

    public static void WriteException(string context, Exception exception)
    {
        Write($"{context}: {exception}");
    }
}
