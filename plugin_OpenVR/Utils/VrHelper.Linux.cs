using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace plugin_OpenVR.Utils;

#if !WINDOWS
internal class VrHelperLinux : IVrHelperPlatform
{
    public ((bool SteamExists, bool VrSettingsExist, bool CopiedDriverExists) Exists,
        (string SteamVrPath, string VrSettingsPath, string CopiedDriverPath) Path) UpdateSteamPaths()
    {
        // Try typical Steam paths
        var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        var steamPath = Path.Combine(home, ".steam");
        if (!Directory.Exists(steamPath))
        {
            // Flatpak or alternative location
            var alt = Path.Combine(home, ".local", "share", "Steam");
            if (Directory.Exists(alt)) steamPath = alt;
        }

        // Find SteamVR runtime from openvrpaths
        var steamVrPath = string.Empty;

        try
        {
            var openVrPaths = OpenVrPaths.Read();
            foreach (var runtimePath in openVrPaths.runtime
                         .Select(runtimePath => new { runtimePath, vrpathreg = Path.Combine(runtimePath, "bin", "linux64", "vrpathreg") })
                         .Where(t => File.Exists(t.vrpathreg))
                         .Select(t => t.runtimePath))
            {
                steamVrPath = runtimePath;
                break;
            }
        }
        catch
        {
            // ignored
        }

        var steamVrSettingsPath = Path.Combine(steamPath ?? "/tmp", "steam", "config", "steamvr.vrsettings");
        // Steam's config on Linux is typically ~/.steam/steam/config/steamvr.vrsettings
        if (!File.Exists(steamVrSettingsPath))
        {
            var altCfg = Path.Combine(home, ".steam", "steam", "config", "steamvr.vrsettings");
            if (File.Exists(altCfg)) steamVrSettingsPath = altCfg;
        }

        var copiedDriverPath = Path.Combine(steamVrPath ?? string.Empty, "drivers",
            SteamVR.Instance?.DriverFolderName ?? "Amethyst");

        return ((
                Directory.Exists(steamPath),
                File.Exists(steamVrSettingsPath),
                Directory.Exists(copiedDriverPath)
            ),
            (
                steamVrPath,
                steamVrSettingsPath,
                copiedDriverPath
            ));
    }

    public bool CloseSteamVr()
    {
        // Try to gracefully close vrmonitor and vrserver
        var any = Process.GetProcesses().Any(p => p.ProcessName == "vrmonitor" || p.ProcessName == "vrserver");
        if (!any) return true;

        foreach (var proc in Process.GetProcesses().Where(p => p.ProcessName == "vrmonitor"))
        {
            try { proc.CloseMainWindow(); }
            catch
            {
                // ignored
            }

            Thread.Sleep(3000);
            try
            {
                if (!proc.HasExited) proc.Kill(true);
            }
            catch
            {
                // ignored
            }
        }

        foreach (var proc in Process.GetProcesses().Where(p => p.ProcessName == "vrserver"))
        {
            try { proc.Kill(true); }
            catch
            {
                // ignored
            }

            Thread.Sleep(3000);
        }

        return true;
    }

    public bool IsOpenVrElevated()
    {
        // Treat Linux as non-elevated by default. If needed, check uid of process.
        try
        {
            var proc = Process.GetProcesses().FirstOrDefault(p => p.ProcessName == "vrserver");
            if (proc is null) return false;
            return IsRootProcess(proc);
        }
        catch { return false; }
    }

    public bool IsCurrentProcessElevated()
    {
        try { return geteuid() == 0; }
        catch { return false; }
    }

    private static bool IsRootProcess(Process proc)
    {
        try
        {
            // On Linux, if effective UID is 0 it's elevated
            return geteuid() == 0;
        }
        catch { return false; }
    }

    [DllImport("libc")]
    private static extern uint geteuid();
}
#endif
