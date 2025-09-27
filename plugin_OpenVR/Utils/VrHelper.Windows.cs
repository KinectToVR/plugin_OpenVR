using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32;

namespace plugin_OpenVR.Utils;

#if WINDOWS
internal class VrHelperWindows : IVrHelperPlatform
{
    public ((bool SteamExists, bool VrSettingsExist, bool CopiedDriverExists) Exists,
        (string SteamVrPath, string VrSettingsPath, string CopiedDriverPath) Path) UpdateSteamPaths()
    {
        var steamPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
            "InstallPath", null)?.ToString();

        var steamVrPath = string.Empty;

        try
        {
            var openVrPaths = OpenVrPaths.Read();
            foreach (var runtimePath in openVrPaths.runtime
                         .Select(runtimePath => new { runtimePath, vrpathreg = Path.Combine(runtimePath, "bin", "win64", "vrpathreg.exe") })
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

        var steamVrSettingsPath = Path.Combine(steamPath ?? "C:/TEMP", "config", "steamvr.vrsettings");
        var copiedDriverPath = Path.Combine(steamVrPath ?? string.Empty, "drivers",
            SteamVR.Instance?.DriverFolderName ?? "Amethyst");

        return ((
                !string.IsNullOrEmpty(steamPath),
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
        if (Process.GetProcesses().FirstOrDefault(proc => proc.ProcessName == "vrserver" || proc.ProcessName == "vrmonitor") == null)
            return true;

        foreach (var process in Process.GetProcesses().Where(proc => proc.ProcessName == "vrmonitor"))
        {
            process.CloseMainWindow();
            Thread.Sleep(5000);
            if (!process.HasExited)
            {
                process.Kill();
                Thread.Sleep(3000);
            }
        }

        foreach (var process in Process.GetProcesses().Where(proc => proc.ProcessName == "vrserver"))
        {
            process.Kill();
            Thread.Sleep(5000);
            if (!process.HasExited)
                return false;
        }

        return true;
    }

    public bool IsOpenVrElevated()
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
        catch
        {
            return true;
        }
    }

    public bool IsCurrentProcessElevated()
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

    private struct TOKEN_ELEVATION
    {
        public uint TokenIsElevated;
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
#endif
