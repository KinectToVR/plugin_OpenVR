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

namespace plugin_OpenVR;

internal class SetupData : ICoreSetupData
{
    public object PluginIcon => new BitmapIcon
    {
        UriSource = new Uri(Path.Join(Directory.GetParent(
                Assembly.GetExecutingAssembly().Location)!.FullName,
            "Assets", "Resources", "icon.png"))
    };

    public string GroupName => string.Empty;
    public Type PluginType => typeof(IServiceEndpoint);
}

internal class DriverInstaller : IDependencyInstaller
{
    public IDependencyInstaller.ILocalizationHost Host { get; set; }

    public List<IDependency> ListDependencies()
    {
        return new List<IDependency>
        {
            new VrDriver
            {
                Host = Host,
                Name = Host?.RequestLocalizedString("/Dependencies/Driver") ?? "OpenVR Driver"
            }
        };
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

        // Create a placeholder for the driver path
        var localAmethystDriverPath = "";

        // Check whether Amethyst is installed as a package
        if (!PackageUtils.IsAmethystPackaged)
        {
            // Optionally change to the other variant
            if (!new DirectoryInfo(localAmethystDriverPath).Exists)
            {
                // Get plugin_OpenVR.dll parent path
                var parentPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location);

                // Search for driver manifests, try max 2 times
                for (var i = 0; i < 2; i++)
                {
                    // Double that to get Amethyst exe path
                    if (parentPath?.Parent != null) parentPath = parentPath.Parent;
                    if (parentPath is null) goto p_search_loop_end;

                    // Find all vr driver manifests there
                    var allLocalDriverManifests = Directory.GetFiles(parentPath.ToString(),
                        "driver.vrdrivermanifest", SearchOption.AllDirectories);

                    // For each found manifest, check if there is an ame driver dll inside
                    foreach (var localDriverManifest in allLocalDriverManifests)
                        if (File.Exists(Path.Combine(Directory.GetParent(localDriverManifest)!.ToString(), "bin",
                                "win64",
                                "driver_Amethyst.dll")))
                        {
                            // We've found it! Now cache it and break free
                            localAmethystDriverPath = Directory.GetParent(localDriverManifest)!.ToString();
                            goto p_search_loop_end;
                        }
                    // Else redo once more & then check
                }
            }

            // End of the searching loop
            p_search_loop_end:

            // If there's none (still), cry about it and abort
            if (string.IsNullOrEmpty(localAmethystDriverPath) || !new DirectoryInfo(localAmethystDriverPath).Exists)
            {
                progress.Report(new InstallationProgress
                {
                    IsIndeterminate = true,
                    StageTitle = Host?.RequestLocalizedString("/CrashHandler/ReRegister/DriverNotFound")!
                });

                return false; // Hide and exit the handler
            }
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
        if (PackageUtils.IsAmethystPackaged)
        {
            // Copy all driver files to Amethyst's local data folder
            new DirectoryInfo(Path.Join(Directory.GetParent(
                    Assembly.GetExecutingAssembly().Location)!.FullName, "Driver", "Amethyst"))
                .CopyToFolder((await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                    "Amethyst", CreationCollisionOption.OpenIfExists)).Path);

            // Assume it's done now and get the path
            localAmethystDriverPath = Path.Join(PackageUtils.GetAmethystAppDataPath(), "Amethyst");

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
            if (externalDriver == localAmethystDriverPath)
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
                                 amethystDriverPath != localAmethystDriverPath))
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
                openVrPaths.external_drivers.Add(localAmethystDriverPath);
                openVrPaths.Write(); // Save it

                // If failed, cry about it and abort
                var openVrPathsCheck = OpenVrPaths.Read();
                if (!openVrPathsCheck.external_drivers.Contains(localAmethystDriverPath))
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
                    new("enable", JsonValue.CreateBooleanValue(true)),
                    new("blocked_by_safe_mode", JsonValue.CreateBooleanValue(false))
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