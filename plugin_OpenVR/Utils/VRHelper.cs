using System;

namespace plugin_OpenVR.Utils;

internal interface IVrHelperPlatform
{
    ((bool SteamExists, bool VrSettingsExist, bool CopiedDriverExists) Exists,
        (string SteamVrPath, string VrSettingsPath, string CopiedDriverPath) Path)
        UpdateSteamPaths();

    bool CloseSteamVr();
    bool IsOpenVrElevated();
    bool IsCurrentProcessElevated();
}

public class VrHelper
{
    private readonly IVrHelperPlatform _impl = s_staticImpl.Value;

    private static readonly Lazy<IVrHelperPlatform> s_staticImpl =
        new(() => OperatingSystem.IsWindows() ? new VrHelperWindows() : new VrHelperLinux());

    public ((bool SteamExists, bool VrSettingsExist, bool CopiedDriverExists) Exists,
        (string SteamVrPath, string VrSettingsPath, string CopiedDriverPath) Path)
        UpdateSteamPaths() => _impl.UpdateSteamPaths();

    public bool CloseSteamVr() => _impl.CloseSteamVr();

    public static bool IsOpenVrElevated() => s_staticImpl.Value.IsOpenVrElevated();

    public static bool IsCurrentProcessElevated() => s_staticImpl.Value.IsCurrentProcessElevated();
}
