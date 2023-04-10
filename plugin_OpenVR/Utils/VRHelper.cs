using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32;

namespace plugin_OpenVR.Utils;

public class VrHelper
{
    public string CopiedDriverPath = "";
    public string SteamPath = "";
    public string SteamVrPath = "";
    public string SteamVrSettingsPath = "";
    public string VrPathReg = "";

    // Returns: <Exists>, <Path> of SteamVR, VRSettings, CopiedDriver
    public ((bool SteamExists, bool VrSettingsExist, bool CopiedDriverExists) Exists,
        (string SteamVrPath, string VrSettingsPath, string CopiedDriverPath) Path)
        UpdateSteamPaths()
    {
        SteamPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
            "InstallPath", null)?.ToString();

        SteamVrPath = "";
        VrPathReg = "";
        try
        {
            var openVrPaths = OpenVrPaths.Read();
            foreach (var runtimePath in openVrPaths.runtime)
            {
                var tempVrPathReg = Path.Combine(runtimePath, "bin", "win64", "vrpathreg.exe");
                if (!File.Exists(tempVrPathReg)) continue;
                SteamVrPath = runtimePath;
                VrPathReg = tempVrPathReg;
            }
        }
        catch (Exception)
        {
            // ignored
        }

        SteamVrSettingsPath = Path.Combine(SteamPath, "config", "steamvr.vrsettings");
        CopiedDriverPath = Path.Combine(SteamVrPath, "drivers", "Amethyst");

        // Return the found-outs
        return ((
                !string.IsNullOrEmpty(SteamPath),
                File.Exists(SteamVrSettingsPath),
                Directory.Exists(CopiedDriverPath)
            ),
            (
                SteamVrPath,
                SteamVrSettingsPath,
                CopiedDriverPath
            ));
    }

    public bool CloseSteamVr()
    {
        // Check if SteamVR is running
        if (Process.GetProcesses()
                .FirstOrDefault(proc => proc.ProcessName == "vrserver" || proc.ProcessName == "vrmonitor") == null)
            return true;

        // Close VrMonitor
        foreach (var process in Process.GetProcesses().Where(proc => proc.ProcessName == "vrmonitor"))
        {
            process.CloseMainWindow();
            Thread.Sleep(5000);
            if (!process.HasExited)
            {
                /* When SteamVR is open with no headset detected,
                    CloseMainWindow will only close the "headset not found" popup
                    so we kill it, if it's still open */
                process.Kill();
                Thread.Sleep(3000);
            }
        }

        // Close VrServer
        /* Apparently, SteamVR server can run without the monitor,
           so we close that, if it's open as well (monitor will complain if you close server first) */
        foreach (var process in Process.GetProcesses().Where(proc => proc.ProcessName == "vrserver"))
        {
            // CloseMainWindow won't work here because it doesn't have a window
            process.Kill();
            Thread.Sleep(5000);
            if (!process.HasExited)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks whether OpenVR is running with admin privileges
    /// </summary>
    public static bool IsOpenVrElevated()
    {
        try
        {
            return Process.GetProcesses().Where(proc => proc.ProcessName == "vrserver")
                .Any(process => !OpenProcessToken(process.Handle, 0x00020000 | 0x0008, out _)
                                && Marshal.GetLastWin32Error() == 5); // Look for any "access denied" errors
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// Returns whether the current process is elevated or not
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCurrentProcessElevated()
    {
        var currentIdentity = WindowsIdentity.GetCurrent();
        var currentGroup = new WindowsPrincipal(currentIdentity);
        return currentGroup.IsInRole(WindowsBuiltInRole.Administrator);
    }
    
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);
}