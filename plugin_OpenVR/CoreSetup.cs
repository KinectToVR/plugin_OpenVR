using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Amethyst.Plugins.Contract;
using Microsoft.UI.Xaml.Controls;
using plugin_OpenVR.Utils;
using Windows.Storage;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace plugin_OpenVR;

internal class SetupData : ICoreSetupData
{
    public object PluginIcon => new PathIcon
    {
        Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry),
            "M36.59,0A36.71,36.71,0,0,0,0,33.77L19.68,41.9a10.3,10.3,0,0,1,5.85-1.8h.57l8.76-12.68c0-.06,0-.12,0-.18A13.85,13.85,0,1,1,48.7,41.1h-.31L35.91,50c0,.16,0,.32,0,.49a10.39,10.39,0,0,1-20.58,2L1.26,46.72A36.71,36.71,0,1,0,36.59,0ZM23,55.71,18.5,53.84a7.72,7.72,0,0,0,4,3.82,7.8,7.8,0,0,0,10.2-4.2,7.79,7.79,0,0,0-9.95-10.28l4.66,1.93A5.74,5.74,0,1,1,23,55.71ZM57.93,27.25a9.23,9.23,0,1,0-9.23,9.23A9.24,9.24,0,0,0,57.93,27.25Zm-16.14,0a6.93,6.93,0,1,1,6.93,6.93A6.93,6.93,0,0,1,41.79,27.23ZM248 48 238.98 23.75 235.9 23.75 246.09 50.58 249.7 50.58 259.84 23.75 256.72 23.75 248 48ZM284.25,31.4c0-4.12-2.23-7.65-9.25-7.65h-8.91V50.58h2.83V40h6.92l6.43,10.6h3.2l-6.8-11.06C282.5,38.35,284.25,35.18,284.25,31.4Zm-8.7,6.07h-6.63V26.22h5.7c4.87,0,6.64,2,6.64,5.52C281.26,34.94,279.33,37.47,275.55,37.47ZM104.77,35.14c-4.06-1.46-6.79-2-6.79-4.22,0-1.84,1.54-2.87,4-2.87a11.91,11.91,0,0,1,6.51,2.06l2.38-4.2a15.41,15.41,0,0,0-9-2.62c-5.75,0-9.75,2.86-9.75,7.85,0,4.43,3,6.33,7.37,7.78,3.81,1.27,6.18,1.88,6.18,3.91,0,1.78-1.55,3-4.82,3a16.49,16.49,0,0,1-7.4-1.91l-1.74,4.65A19,19,0,0,0,101.16,51c6.18,0,10.48-3.07,10.48-8.54C111.64,38.5,109.14,36.68,104.77,35.14ZM116.17 28.57 124.33 28.57 124.33 50.58 129.95 50.58 129.95 28.57 138.08 28.57 138.08 23.75 116.17 23.75 116.17 28.57ZM143.9 50.58 161.99 50.58 161.99 45.71 149.52 45.71 149.52 39.41 160.27 39.41 160.27 34.6 149.52 34.6 149.52 28.55 161.99 28.55 161.99 23.75 143.9 23.75 143.9 50.58ZM176.93,23.75,166.87,50.58h5.89l1.77-5.21H185l1.81,5.21h6.09L182.58,23.75Zm-6.4,17.14,3.66-10.74,3.74,10.74ZM212.62 43.11 203.6 23.75 198.24 23.75 198.24 50.58 203.62 50.58 203.62 34.26 210.84 49.79 214.01 49.79 221.35 34.12 221.35 50.58 226.73 50.58 226.73 23.75 221.31 23.75 212.62 43.11ZM286.89 24.35 288.58 24.35 288.58 28.9 289.26 28.9 289.26 24.35 290.95 24.35 290.95 23.75 286.89 23.75 286.89 24.35ZM296.43 23.75 294.54 27.97 292.59 23.75 291.9 23.75 291.9 28.9 292.57 28.9 292.57 25.09 294.32 28.84 294.69 28.84 296.45 25.09 296.45 28.9 297.12 28.9 297.12 23.75 296.43 23.75Z")
    };

    public string GroupName => string.Empty;
    public Type PluginType => typeof(IServiceEndpoint);
}

internal class DriverInstaller : IDependencyInstaller
{
    public IDependencyInstaller.ILocalizationHost Host { get; set; }

    public List<IDependency> ListDependencies()
    {
        List<IDependency> dep =
        [
            new VrDriver
            {
                Host = Host,
                Name = Host?.RequestLocalizedString("/Dependencies/Driver") ?? "OpenVR Driver"
            }
        ];

        if (SteamVR.Instance is not null)
            dep.Add(new NullDriver
            {
                Host = Host,
                Name = Host?.RequestLocalizedString("/Dependencies/Null") ?? "Null Driver"
            });

        return dep;
    }

    public List<IFix> ListFixes()
    {
        return [];
    }
}

internal class VrDriver : IDependency
{
    public IDependencyInstaller.ILocalizationHost Host { get; set; }

    public string Name { get; set; }
    public bool IsMandatory => true;
    public bool IsInstalled => false;
    public string InstallerEula => string.Empty;

    public async Task<bool> Install(IProgress<InstallationProgress> progress, CancellationToken cancellationToken)
    {
        // Amethyst will handle this exception for us anyway
        cancellationToken.ThrowIfCancellationRequested();

        VrHelper helper = new();
        OpenVrPaths openVrPaths;
        var resultPaths = helper.UpdateSteamPaths();

        try // Try-Catch it
        {
            // Check if SteamVR was found
            if (!resultPaths.Exists.SteamExists)
            {
                Directory.CreateDirectory(Directory.GetParent(OpenVrPaths.Path)!.FullName);
                File.Create(OpenVrPaths.Path);
            }

            // Read the OpenVRPaths
            openVrPaths = OpenVrPaths.Read();
        }
        catch (Exception)
        {
            progress.Report(new InstallationProgress
            {
                IsIndeterminate = true,
                StageTitle = Host?.RequestLocalizedString("/CrashHandler/ReRegister/OpenVRPathsError")!
            });
            return false;
        }

        /*
         * ReRegister Logic:
         *
         * Search for Amethyst VRDriver in the crash handler's directory
         * and 2 folders up in tree, recursively. (Find the manifest)
         *
         * If the manifest & dll are found, check and ask to close SteamVR
         *
         * With closed SteamVR, search for all remaining 'driver_Amethyst' instances:
         * copied inside /drivers/ or registered. If found, ask to delete them
         *
         * When everything is purified, we can register the 'driver_Amethyst'
         * via OpenVRPaths and then check twice if it's there ready to go
         *
         * If the previous steps succeeded, we can enable the 'driver_Amethyst'
         * in VRSettings. A run failure/exception of this one isn't critical
         */

        /* 1 */

        try
        {
            await PathsHandler.Setup();
            SteamVR.Instance?.Shutdown();
        }
        catch (Exception)
        {
            // Ignored
        }

        /* 2 */

        // Force exit (kill) SteamVR
        if (Process.GetProcesses().FirstOrDefault(proc => proc.ProcessName is "vrserver" or "vrmonitor") != null)
        {
            // Check for privilege mismatches
            if (VrHelper.IsOpenVrElevated() && !VrHelper.IsCurrentProcessElevated())
            {
                progress.Report(new InstallationProgress
                {
                    IsIndeterminate = true,
                    StageTitle = Host?.RequestLocalizedString("/CrashHandler/ReRegister/Elevation")!
                });

                return false; // Hide and exit the handler
            }

            // Finally kill
            await Task.Factory.StartNew(helper.CloseSteamVr, cancellationToken);
        }

        /* 1.1 Copy packaged Amethyst drivers */

        // Check whether Amethyst is installed as a package
        // Copy all driver files to Amethyst's local data folder
        new DirectoryInfo(Path.Join(Directory.GetParent(
                Assembly.GetExecutingAssembly().Location)!.FullName, "Driver", "Amethyst"))
            .CopyToFolder((await PathsHandler.LocalFolder.CreateFolderAsync(
                "Amethyst", CreationCollisionOption.OpenIfExists)).Path);

        // Assume it's done now and get the path
        var localAmethystDriverPath = Path.Join(PathsHandler.LocalFolder.Path, "Amethyst");

        // If there's none (still), cry about it and abort
        if (string.IsNullOrEmpty(localAmethystDriverPath) || !Directory.Exists(localAmethystDriverPath))
        {
            Host?.Log($"Copied driver not present at expectant path of: {localAmethystDriverPath}");
            progress.Report(new InstallationProgress
            {
                IsIndeterminate = true,
                StageTitle = Host?.RequestLocalizedString("/CrashHandler/ReRegister/DriverNotFound")!
            });

            return false; // Hide and exit the handler
        }

        /* 2.5 */

        // Search for all K2EX instances and either unregister or delete them

        var isDriverK2Present = resultPaths.Exists.CopiedDriverExists; // is ame copied?
        var driverK2PathsList = new List<string>(); // ame external list

        foreach (var externalDriver in openVrPaths.external_drivers
                     .Where(externalDriver => externalDriver.Contains("KinectToVR")))
        {
            isDriverK2Present = true;
            driverK2PathsList.Add(externalDriver);
        }

        // Try-Catch it
        try
        {
            if (isDriverK2Present)
            {
                // Delete the copied K2EX Driver (if exists)
                if (resultPaths.Exists.CopiedDriverExists)
                    Directory.Delete(resultPaths.Path.CopiedDriverPath, true); // Delete

                // Un-register any remaining K2EX Drivers (if exist)
                if (driverK2PathsList.Any())
                {
                    foreach (var driverK2Path in driverK2PathsList) openVrPaths.external_drivers.Remove(driverK2Path);

                    // Save it
                    openVrPaths.Write();
                }
            }
        }
        catch (Exception)
        {
            progress.Report(new InstallationProgress
            {
                IsIndeterminate = true,
                StageTitle = Host?.RequestLocalizedString("/CrashHandler/ReRegister/FatalRemoveException_K2EX")!
            });

            return false; // Hide and exit the handler
        }

        /* 3 */

        // Search for all remaining (registered or copied) Amethyst Driver instances

        var isAmethystDriverPresent = resultPaths.Exists.CopiedDriverExists; // is ame copied?
        var amethystDriverPathsList = new List<string>(); // ame external list

        var isLocalAmethystDriverRegistered = false; // is our local ame registered?

        foreach (var externalDriver in openVrPaths.external_drivers
                     .Where(externalDriver => externalDriver.Contains("Amethyst")))
        {
            // Don't un-register the already-existent one
            if (externalDriver == localAmethystDriverPath ||
                externalDriver == localAmethystDriverPath.ShortPath())
            {
                isLocalAmethystDriverRegistered = true;
                continue; // Don't report it
            }

            isAmethystDriverPresent = true;
            amethystDriverPathsList.Add(externalDriver);
        }

        // Try-Catch it
        try
        {
            if (isAmethystDriverPresent)
            {
                // Delete the copied Amethyst Driver (if exists)
                if (resultPaths.Exists.CopiedDriverExists)
                    Directory.Delete(resultPaths.Path.CopiedDriverPath, true); // Delete

                // Un-register any remaining Amethyst Drivers (if exist)
                if (amethystDriverPathsList.Any())
                {
                    foreach (var amethystDriverPath in amethystDriverPathsList.Where(amethystDriverPath =>
                                 amethystDriverPath != localAmethystDriverPath &&
                                 amethystDriverPath != localAmethystDriverPath.ShortPath()))
                        openVrPaths.external_drivers.Remove(amethystDriverPath); // Un-register

                    // Save it
                    openVrPaths.Write();
                }
            }
        }
        catch (Exception)
        {
            progress.Report(new InstallationProgress
            {
                IsIndeterminate = true,
                StageTitle = Host?.RequestLocalizedString("/CrashHandler/ReRegister/FatalRemoveException")!
            });

            return false; // Hide and exit the handler
        }

        /* 4 */

        // If out local amethyst driver was already registered, skip this step
        if (!isLocalAmethystDriverRegistered)
            try // Try-Catch it
            {
                // Register the local Amethyst Driver via OpenVRPaths
                openVrPaths.external_drivers.Add(localAmethystDriverPath.ShortPath());
                openVrPaths.Write(); // Save it

                // If failed, cry about it and abort
                var openVrPathsCheck = OpenVrPaths.Read();
                if (!openVrPathsCheck.external_drivers.Contains(localAmethystDriverPath) &&
                    !openVrPathsCheck.external_drivers.Contains(localAmethystDriverPath.ShortPath()))
                {
                    progress.Report(new InstallationProgress
                    {
                        IsIndeterminate = true,
                        StageTitle = Host?.RequestLocalizedString("/CrashHandler/ReRegister/OpenVRPathsWriteError")!
                    });

                    return false; // Hide and exit the handler
                }
            }
            catch (Exception)
            {
                progress.Report(new InstallationProgress
                {
                    IsIndeterminate = true,
                    StageTitle = Host?.RequestLocalizedString("/CrashHandler/ReRegister/FatalRegisterException")!
                });

                return false; // Hide and exit the handler
            }

        /* 5 */

        // Try-Catch it
        try
        {
            // Read the vr settings
            var steamVrSettings =
                JsonObject.Parse(await File.ReadAllTextAsync(resultPaths.Path.VrSettingsPath, cancellationToken));

            // Enable & unblock the Amethyst Driver
            steamVrSettings.Remove("driver_Amethyst");
            steamVrSettings.Add("driver_Amethyst",
                new JsonObject
                {
                    new KeyValuePair<string, IJsonValue>("enable", JsonValue.CreateBooleanValue(true)),
                    new KeyValuePair<string, IJsonValue>("blocked_by_safe_mode", JsonValue.CreateBooleanValue(false))
                });

            await File.WriteAllTextAsync(resultPaths.Path.VrSettingsPath, steamVrSettings.ToString(),
                cancellationToken);
        }
        catch (Exception)
        {
            // Not critical
        }

        // Winning it!
        return true;
    }
}

internal class NullDriver : IDependency
{
    public IDependencyInstaller.ILocalizationHost Host { get; set; }

    public string Name { get; set; }
    public bool IsMandatory => false;
    public bool IsInstalled => false;
    public string InstallerEula => string.Empty;

    public async Task<bool> Install(IProgress<InstallationProgress> progress, CancellationToken cancellationToken)
    {
        // Amethyst will handle this exception for us anyway
        cancellationToken.ThrowIfCancellationRequested();

        VrHelper helper = new();
        var resultPaths = helper.UpdateSteamPaths();

        try // Try-Catch it
        {
            // Check if SteamVR was found
            if (!resultPaths.Exists.SteamExists)
            {
                Directory.CreateDirectory(Directory.GetParent(OpenVrPaths.Path)!.FullName);
                File.Create(OpenVrPaths.Path);
            }
        }
        catch (Exception)
        {
            progress.Report(new InstallationProgress
            {
                IsIndeterminate = true,
                StageTitle = Host?.RequestLocalizedString("/CrashHandler/ReRegister/OpenVRPathsError")!
            });
            return false;
        }

        /* 1 */

        try
        {
            await PathsHandler.Setup();
            SteamVR.Instance?.Shutdown();
        }
        catch (Exception)
        {
            // Ignored
        }

        /* 2 */

        // Force exit (kill) SteamVR
        if (Process.GetProcesses().FirstOrDefault(proc => proc.ProcessName is "vrserver" or "vrmonitor") != null)
        {
            // Check for privilege mismatches
            if (VrHelper.IsOpenVrElevated() && !VrHelper.IsCurrentProcessElevated())
            {
                progress.Report(new InstallationProgress
                {
                    IsIndeterminate = true,
                    StageTitle = Host?.RequestLocalizedString("/CrashHandler/ReRegister/Elevation")!
                });

                return false; // Hide and exit the handler
            }

            // Finally kill
            await Task.Factory.StartNew(helper.CloseSteamVr, cancellationToken);
        }

        /* 3 */

        // Try-Catch it
        try
        {
            // Read the vr settings
            var steamVrSettings =
                JsonObject.Parse(await File.ReadAllTextAsync(resultPaths.Path.VrSettingsPath, cancellationToken));

            // Enable & unblock the Null Driver
            steamVrSettings.Remove("driver_null");
            steamVrSettings.Add("driver_null",
                new JsonObject
                {
                    new KeyValuePair<string, IJsonValue>("displayFrequency", JsonValue.CreateNumberValue(60)),
                    new KeyValuePair<string, IJsonValue>("enable", JsonValue.CreateBooleanValue(true)),
                    new KeyValuePair<string, IJsonValue>("id", JsonValue.CreateStringValue("Null Driver")),
                    new KeyValuePair<string, IJsonValue>("renderHeight", JsonValue.CreateNumberValue(0)),
                    new KeyValuePair<string, IJsonValue>("renderWidth", JsonValue.CreateNumberValue(0)),
                    new KeyValuePair<string, IJsonValue>("secondsFromVsyncToPhotons", JsonValue.CreateNumberValue(0.10000000149011612)),
                    new KeyValuePair<string, IJsonValue>("serialNumber", JsonValue.CreateStringValue("Null 4711")),
                    new KeyValuePair<string, IJsonValue>("windowHeight", JsonValue.CreateNumberValue(0)),
                    new KeyValuePair<string, IJsonValue>("windowWidth", JsonValue.CreateNumberValue(0)),
                    new KeyValuePair<string, IJsonValue>("windowX", JsonValue.CreateNumberValue(0)),
                    new KeyValuePair<string, IJsonValue>("windowY", JsonValue.CreateNumberValue(0))
                });

            steamVrSettings.Remove("steamvr");
            steamVrSettings.Add("steamvr",
                new JsonObject
                {
                    new KeyValuePair<string, IJsonValue>("activateMultipleDrivers", JsonValue.CreateBooleanValue(true)),
                    new KeyValuePair<string, IJsonValue>("enableHomeApp", JsonValue.CreateBooleanValue(false)),
                    new KeyValuePair<string, IJsonValue>("forcedDriver", JsonValue.CreateStringValue("null")),
                    new KeyValuePair<string, IJsonValue>("mirrorViewGeometry", JsonValue.CreateStringValue("0 0 960 540"))
                });

            await File.WriteAllTextAsync(resultPaths.Path.VrSettingsPath, steamVrSettings.ToString(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            progress.Report(new InstallationProgress
            {
                IsIndeterminate = true,
                StageTitle = ex.Message
            });

            return false; // Hide and exit the handler
        }

        // Winning it!
        return true;
    }
}