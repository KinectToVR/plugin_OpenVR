using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Principal;
using System.Text;
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
        CopiedDriverPath = Path.Combine(SteamVrPath, "drivers", SteamVR.Instance?.DriverFolderName ?? "Amethyst");

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
            var process = Process.GetProcesses().FirstOrDefault(proc => proc.ProcessName == "vrserver", null);
            if (process is null) return false;

            var handle = OpenProcess(process, ProcessAccessFlags.QueryLimitedInformation);
            if (!OpenProcessToken(handle, TOKEN_QUERY, out var token)) return true;

            GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenElevation,
                IntPtr.Zero, 0, out var length);

            var elevation = Marshal.AllocHGlobal((int)length);
            if (!GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenElevation,
                    elevation, length, out _)) return true;

            return Marshal.PtrToStructure<TOKEN_ELEVATION>(elevation).TokenIsElevated != 0;
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
    private static extern bool GetTokenInformation(
        IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass,
        IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

    private static IntPtr OpenProcess(Process proc, ProcessAccessFlags flags)
    {
        return OpenProcess((uint)flags, false, (uint)proc.Id);
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    private static bool OpenProcessToken(IntPtr ProcessHandle, ProcessAccessFlags flags, out IntPtr TokenHandle)
    {
        return OpenProcessToken(ProcessHandle, (uint)flags, out TokenHandle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool QueryFullProcessImageName(
        [In] IntPtr hProcess,
        [In] int dwFlags,
        [Out] StringBuilder lpExeName,
        ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        ProcessAccessFlags processAccess,
        bool bInheritHandle,
        int processId);

    private struct TOKEN_ELEVATION
    {
        public uint TokenIsElevated;
    }

    public static string GetProcessFilename(Process p)
    {
        try
        {
            var capacity = 2000;
            var builder = new StringBuilder(capacity);
            var ptr = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, p.Id);
            return !QueryFullProcessImageName(ptr, 0, builder, ref capacity) ? null : builder.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public readonly int dwProcessId;
        public readonly FILETIME ProcessStartTime;
    }

    private enum RM_APP_TYPE
    {
        RmUnknownApp = 0,
        RmMainWindow = 1,
        RmOtherWindow = 2,
        RmService = 3,
        RmExplorer = 4,
        RmConsole = 5,
        RmCritical = 1000
    }

    private enum TOKEN_INFORMATION_CLASS
    {
        TokenUser = 1,
        TokenGroups,
        TokenPrivileges,
        TokenOwner,
        TokenPrimaryGroup,
        TokenDefaultDacl,
        TokenSource,
        TokenType,
        TokenImpersonationLevel,
        TokenStatistics,
        TokenRestrictedSids,
        TokenSessionId,
        TokenGroupsAndPrivileges,
        TokenSessionReference,
        TokenSandBoxInert,
        TokenAuditPolicy,
        TokenOrigin,
        TokenElevationType,
        TokenLinkedToken,
        TokenElevation,
        TokenHasRestrictions,
        TokenAccessInformation,
        TokenVirtualizationAllowed,
        TokenVirtualizationEnabled,
        TokenIntegrityLevel,
        TokenUIAccess,
        TokenMandatoryPolicy,
        TokenLogonSid,
        MaxTokenInfoClass
    }

    private const uint TOKEN_QUERY = 0x0008;

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        QueryLimitedInformation = 0x00001000
    }
}