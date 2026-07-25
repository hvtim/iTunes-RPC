using System.Diagnostics;

namespace iTunesRPC;

// Manages the Startup-folder shortcut so the "start at login" checkbox in
// Settings can toggle autorun directly, without needing to rerun the installer.
public static class AutoLaunch
{
    private static readonly string ShortcutPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "iTunes-RPC.lnk");

    public static bool IsEnabled => File.Exists(ShortcutPath);

    public static void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
            return;
        }

        string exePath = Process.GetCurrentProcess().MainModule?.FileName
            ?? Path.Combine(AppContext.BaseDirectory, "iTunesRPC.exe");
        string installDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) return;

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(ShortcutPath);
        shortcut.TargetPath = exePath;
        shortcut.WorkingDirectory = installDir;
        shortcut.Description = "iTunes now-playing sync for Discord Rich Presence";
        shortcut.Save();
    }
}
