using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Amethyst.Contract;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using plugin_OpenVR.Utils;
using Valve.VR;

namespace plugin_OpenVR.Pages;

public sealed partial class SettingsPage : UserControl, INotifyPropertyChanged
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    public IAmethystHost Host { get; set; }
    public SteamVR DataParent { get; set; }
    private ActionsManager Manager { get; set; }

    public bool PageLoaded { get; set; } = false;

    public bool IsStandableSupportEnabled
    {
        get => Host?.PluginSettings.GetSetting("StandableSupport", false) ?? false;
        set
        {
            if (Host?.PluginSettings is null || !PageLoaded) return;
            Host?.PluginSettings.SetSetting("StandableSupport", value);

            if (DataParent is null) return;
            DataParent.IsStandableSupportEnabled = value;
        }
    }

    private void ReManifestButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ReManifestButton.Resources["ActionFailedFlyout"] is not Flyout actionFailedFlyout) return;
        switch (InstallVrApplicationManifest())
        {
            // Not found failure
            case -2:
            {
                actionFailedFlyout.Content = new TextBlock
                {
                    FontWeight = FontWeight.SemiBold, Text = Host.RequestLocalizedString("/SettingsPage/ReManifest/Error/NotFound")
                };

                actionFailedFlyout.ShowAt(ReManifestButton);
                break;
            }

            // SteamVR failure
            case 1:
            {
                actionFailedFlyout.Content = new TextBlock
                {
                    FontWeight = FontWeight.SemiBold, Text = Host.RequestLocalizedString("/SettingsPage/ReManifest/Error/Other")
                };

                actionFailedFlyout.ShowAt(ReManifestButton);
                break;
            }
        }

        // Play a sound
        Host?.PlayAppSound(SoundType.Invoke);
    }

    public int InstallVrApplicationManifest()
    {
        if (!SteamVR.Initialized || OpenVR.Applications is null) return 0; // Sanity check

        try
        {
            // Prepare the manifest by copying it to a shared directory
            // Copy all driver files to Amethyst's local data folder
            Directory.CreateDirectory(Path.Join(Host.PathHelper.LocalFolder.FullName, DataParent.DriverFolderName));

            // Copy the manifest
            new FileInfo(Path.Join(
                    Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.FullName, "Amethyst.vrmanifest"))
                .CopyTo(Path.Join(Host.PathHelper.LocalFolder.FullName, DataParent.DriverFolderName, "Amethyst.vrmanifest"), true);

            // Copy the icon
            var icon = new FileInfo(Path.Join(
                Directory.GetParent(Environment.ProcessPath!)!.FullName, "Assets", "Visuals", "ktvr.png"));

            if (icon.Exists)
                icon.CopyTo(Path.Join(Host.PathHelper.LocalFolder.FullName, DataParent.DriverFolderName, "ktvr.png"), true);

            // Assume it's done now and get the path
            var copiedManifestPath =
                Path.Join(Host.PathHelper.LocalFolder.FullName, DataParent.DriverFolderName, "Amethyst.vrmanifest");

            // If there's none (still), cry about it and abort
            if (string.IsNullOrEmpty(copiedManifestPath) || !File.Exists(copiedManifestPath))
            {
                // Hide the "working" progress bar
                ReRegisterButtonBar.Opacity = 0.0;

                Host?.Log($"Copied driver not present at expectant path of: {copiedManifestPath}");
                Host?.Log($"Amethyst vr manifest ({copiedManifestPath}) not found!", LogSeverity.Warning);
                return -2;
            }

            if (OpenVR.Applications.IsApplicationInstalled("K2VR.Amethyst"))
            {
                Host.Log("Amethyst manifest is already installed, removing...");

                OpenVR.Applications.RemoveApplicationManifest(
                    "C:/Program Files/ModifiableWindowsApps/K2VRTeam.Amethyst.App/Plugins/plugin_OpenVR/Amethyst.vrmanifest");
                OpenVR.Applications.RemoveApplicationManifest("../../Plugins/plugin_OpenVR/Amethyst.vrmanifest");
            }

            // Compose the manifest path depending on where our plugin is
            var manifestPath = Path.Join(Host.PathHelper.LocalFolder.FullName,
                DataParent.DriverFolderName, "Amethyst.vrmanifest");

            if (File.Exists(manifestPath))
            {
                var manifestJson = JsonConvert.DeserializeObject<VRManifest>(File.ReadAllText(manifestPath));
                if (manifestJson?.applications?.FirstOrDefault() is null)
                {
                    Host.Log($"Amethyst vr manifest ({manifestPath}) was invalid!", LogSeverity.Warning);
                    return -2; // Give up on registering the application vr manifest
                }

                try
                {
                    manifestJson.applications.FirstOrDefault()!.launch_type = "binary"; // Modify the manifest
                }
                catch (InvalidOperationException e)
                {
                    // In case of any issues, replace the computed path with the default, relative one
                    manifestJson.applications.FirstOrDefault()!.launch_type = "binary"; // Launch exe
                    Host?.Log(e); // This will throw in case of not packaged apps, so don't care too much
                }

                // Write the modified manifest data to the actual file
                File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifestJson, Formatting.Indented));

                // Finally register the manifest
                var appError = OpenVR.Applications.AddApplicationManifest(manifestPath, false);
                if (appError != EVRApplicationError.None)
                {
                    Host?.Log($"Amethyst manifest not installed! Error: {appError}", LogSeverity.Warning);
                    return -1;
                }

                Host?.Log("Amethyst manifest installed at: " + $"{manifestPath}");
                return 0;
            }
        }
        catch (Exception e)
        {
            Host?.Log(e, LogSeverity.Error);
        }

        Host?.Log("Amethyst vr manifest not found!", LogSeverity.Warning);
        return -2;
    }

    public async void ReRegisterButton_OnClick(object o, RoutedEventArgs routedEventArgs)
    {
        // Play a sound
        Host?.PlayAppSound(SoundType.Invoke);

        VrHelper helper = new();
        OpenVrPaths openVrPaths;
        var resultPaths = helper.UpdateSteamPaths();

        // Check if SteamVR was found
        if (!resultPaths.Exists.SteamExists)
        {
            // Critical, cry about it
            await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/SteamVRNotFound"), "", "");
            return;
        }

        try // Try-Catch it
        {
            // Read the OpenVRPaths
            openVrPaths = OpenVrPaths.Read();
        }
        catch (Exception)
        {
            // Critical, cry about it
            await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/OpenVRPathsError"), "", "");
            return;
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

        // Show the "working" progress bar
        ReRegisterButtonBar.Opacity = 1.0;

        /* 1 */

        // Not required anymore

        /* 2 */

        // Force exit (kill) SteamVR
        if (Process.GetProcesses().FirstOrDefault(proc => proc.ProcessName is "vrserver" or "vrmonitor") != null)
        {
            // Check for privilege mismatches
            if (VrHelper.IsOpenVrElevated() && !VrHelper.IsCurrentProcessElevated())
            {
                // Hide the "working" progress bar
                ReRegisterButtonBar.Opacity = 0.0;

                await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/Elevation"), "", "");
                return; // Hide and exit the handler
            }

            // Finally kill
            if (await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/KillSteamVR/Content"),
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/KillSteamVR/PrimaryButton"),
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/KillSteamVR/SecondaryButton")))
            {
                await Task.Factory.StartNew(() =>
                {
                    DataParent?.Shutdown(); // Exit not to be killed
                    Host.RefreshStatusInterface();
                    return helper.CloseSteamVr();
                });
            }
            else
            {
                ReRegisterButtonBar.Opacity = 0.0;
                return; // Hide and exit the handler
            }
        }

        /* 1.1 Copy packaged Amethyst drivers */

        // Copy all driver files to Amethyst's local data folder
        new DirectoryInfo(Path.Join(Directory.GetParent(
                Assembly.GetExecutingAssembly().Location)!.FullName, "Driver", DataParent.DriverFolderName))
            .CopyToFolder((await (await Host!.StorageProvider.TryGetFolderFromPathAsync(
                new Uri(Host!.PathHelper.LocalFolder.FullName)))!.CreateFolderAsync(DataParent.DriverFolderName))!.Path.AbsolutePath);

        // Assume it's done now and get the path
        var localAmethystDriverPath = Path.Join(Host.PathHelper.LocalFolder.FullName, DataParent.DriverFolderName);

        // If there's none (still), cry about it and abort
        if (string.IsNullOrEmpty(localAmethystDriverPath) || !Directory.Exists(localAmethystDriverPath))
        {
            // Hide the "working" progress bar
            ReRegisterButtonBar.Opacity = 0.0;

            Host?.Log($"Copied driver not present at expectant path of: {localAmethystDriverPath}");
            await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/DriverNotFound"), "", "");
            return; // Hide and exit the handler
        }

        /* 2.5 */

        // Search for all K2EX instances and either unregister or delete them

        var isDriverK2Present = resultPaths.Exists.CopiedDriverExists; // is ame copied?
        var driverK2PathsList = new List<string>(); // ame external list

        foreach (var externalDriver in openVrPaths.external_drivers.Where(externalDriver =>
                     externalDriver.Contains("KinectToVR")))
        {
            isDriverK2Present = true;
            driverK2PathsList.Add(externalDriver);
        }

        // Remove (or delete) the existing K2EX Drivers
        if (isDriverK2Present && await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/ExistingDrivers/Content_K2EX"),
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/ExistingDrivers/PrimaryButton_K2EX"),
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/ExistingDrivers/SecondaryButton_K2EX"))) return;

        // Try-Catch it
        try
        {
            if (isDriverK2Present || resultPaths.Exists.CopiedDriverExists)
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
            // Hide the "working" progress bar
            ReRegisterButtonBar.Opacity = 0.0;

            // Critical, cry about it
            await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/FatalRemoveException_K2EX"), "", "");
            return; // Hide and exit the handler
        }

        /* 3 */

        // Search for all remaining (registered or copied) Amethyst Driver instances

        var isAmethystDriverPresent = resultPaths.Exists.CopiedDriverExists; // is ame copied?
        var amethystDriverPathsList = new List<string>(); // ame external list

        var isLocalAmethystDriverRegistered = false; // is our local ame registered?

        foreach (var externalDriver in openVrPaths.external_drivers.Where(externalDriver =>
                     externalDriver.Contains("Amethyst")))
        {
            // Don't un-register the already-existent one
            if (externalDriver == localAmethystDriverPath ||
                externalDriver == localAmethystDriverPath.ShortPath())
            {
                isLocalAmethystDriverRegistered = true;
                continue; // Don't report it
            }

            isAmethystDriverPresent = !externalDriver.StartsWith(Host.PathHelper.LocalFolder.FullName);
            amethystDriverPathsList.Add(externalDriver);
        }

        // Remove (or delete) the existing Amethyst Drivers
        if (isAmethystDriverPresent && !await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/ExistingDrivers/Content"),
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/ExistingDrivers/PrimaryButton"),
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/ExistingDrivers/SecondaryButton"))) return;

        // Try-Catch it
        try
        {
            if (isAmethystDriverPresent || amethystDriverPathsList.Any() || resultPaths.Exists.CopiedDriverExists)
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
            // Hide the "working" progress bar
            ReRegisterButtonBar.Opacity = 0.0;

            // Critical, cry about it
            await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/FatalRemoveException"), "", "");
            return; // Hide and exit the handler
        }

        /* 4 */

        // If out local amethyst driver was already registered, skip this step
        if (!isLocalAmethystDriverRegistered)
        {
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
                    // Hide the "working" progress bar
                    ReRegisterButtonBar.Opacity = 0.0;

                    await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                        Host?.RequestLocalizedString("/CrashHandler/ReRegister/OpenVRPathsWriteError"), "", "");
                    return; // Hide and exit the handler
                }
            }
            catch (Exception)
            {
                // Hide the "working" progress bar
                ReRegisterButtonBar.Opacity = 0.0;

                // Critical, cry about it
                await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/FatalRegisterException"), "", "");
                return; // Hide and exit the handler
            }
        }

        /* 5 */

        // Try-Catch it
        try
        {
            // Read the vr settings
            var steamVrSettings = JObject.Parse(await File.ReadAllTextAsync(resultPaths.Path.VrSettingsPath));

            // Enable & unblock the Amethyst Driver
            steamVrSettings.Remove($"driver_{SteamVR.Instance.DriverFolderName}");
            steamVrSettings.Add($"driver_{SteamVR.Instance.DriverFolderName}", JObject.FromObject(
                new { enable = true, blocked_by_safe_mode = false }));

            await File.WriteAllTextAsync(resultPaths.Path.VrSettingsPath, steamVrSettings.ToString());
        }
        catch (Exception)
        {
            // Not critical
        }

        // Hide the "working" progress bar
        ReRegisterButtonBar.Opacity = 0.0;

        // Winning it!
        await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
            Host?.RequestLocalizedString("/CrashHandler/ReRegister/Finished"), "", "");
    }

    private void SettingsPage_OnLoaded(object sender, RoutedEventArgs routedEventArgs)
    {
        PageLoaded = true;

        if (StandableToggleSwitch is not null)
            StandableToggleSwitch.IsChecked = IsStandableSupportEnabled;
    }
    
    private void ManagerButton_OnClick(object sender, RoutedEventArgs e)
    {
        Manager ??= new ActionsManager
        {
            Host = Host,
            DataParent = DataParent
        };
        
        Host?.ShowDialog(Manager);
    }
}
