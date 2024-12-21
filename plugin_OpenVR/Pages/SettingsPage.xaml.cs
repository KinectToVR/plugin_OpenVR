using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources.Core;
using Windows.Data.Json;
using Windows.Storage;
using Amethyst.Plugins.Contract;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Newtonsoft.Json;
using plugin_OpenVR.Utils;
using Valve.VR;
using Microsoft.UI.Xaml.Media.Animation;

namespace plugin_OpenVR.Pages;

public sealed partial class SettingsPage : UserControl, INotifyPropertyChanged
{
    public SettingsPage()
    {
        var pluginDir = Directory.GetParent(Assembly.GetAssembly(GetType())!.Location);

        var priFile = StorageFile.GetFileFromPathAsync(
            Path.Join(pluginDir!.FullName, "resources.pri")).GetAwaiter().GetResult();

        ResourceManager.Current.LoadPriFiles([priFile]);
        ResourceManager.Current.LoadPriFiles([priFile]);

        Application.LoadComponent(this, new Uri($"ms-appx:///{Path.Join(pluginDir!.FullName, "Pages", $"{GetType().Name}.xaml")}"),
            ComponentResourceLocation.Application);
    }

    private bool _listViewChangeBlock = false;
    public bool IsAddingNewAction { get; set; }
    public bool IsAddingNewActionInverse => !IsAddingNewAction;

    public IAmethystHost Host { get; set; }
    public SteamVR DataParent { get; set; }
    public InputAction TreeSelectedAction { get; set; }

    public string SelectedActionName
    {
        get => IsAddingNewAction && TreeSelectedAction is not null
            ? NewActionName
            : TreeSelectedAction?.NameLocalized ?? GetString("/InputActions/Title/NoSelection");
        set
        {
            if (!IsAddingNewAction || TreeSelectedAction is null) return;
            NewActionName = value;
            OnPropertyChanged();
        }
    }

    public string SelectedActionDescription => TreeSelectedAction?.Name ?? string.Empty;
    public bool SelectedActionValid => TreeSelectedAction?.Valid ?? false;
    public bool SelectedActionInvalid => !SelectedActionValid;
    public bool ActionValid => TreeSelectedAction is not null && (!IsAddingNewAction || !string.IsNullOrEmpty(SelectedActionName));
    public string NewActionName { get; set; }

    public IEnumerable<InputAction> CustomActions =>
        DataParent.VrInput.RegisteredActions.Actions.Where(x => x.Custom);

    public string SelectedActionCode
    {
        get => TreeSelectedAction?.Code ?? string.Empty;
        set
        {
            if (TreeSelectedAction is null) return;
            TreeSelectedAction.Code = value;
        }
    }

    private string GetString(string key)
    {
        return Host?.RequestLocalizedString(key) ?? key;
    }

    private void ActionFailedFlyout_OnOpening(object sender, object e)
    {
        Host?.PlayAppSound(SoundType.Show);
    }

    private void ActionFailedFlyout_OnClosing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
    {
        Host?.PlayAppSound(SoundType.Hide);
    }

    private void ActionsFlyout_OnOpening(object sender, object e)
    {
        Host?.PlayAppSound(SoundType.Show);
        ReloadActions();
    }

    private void ReloadActions()
    {
        TreeSelectedAction = null;
        IsAddingNewAction = !CustomActions.Any();
        if (IsAddingNewAction)
        {
            TreeSelectedAction = new InputAction(
                $"/actions/default/in/{Guid.NewGuid().ToString().ToUpper()}",
                "boolean", "optional");

            NewActionName = string.Empty;
        }

        ActionsListView.SelectionMode = ListViewSelectionMode.None;
        ActionsListView.SelectionMode = ListViewSelectionMode.Single;

        OnPropertyChanged();
    }

    private void ActionsFlyout_OnClosing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
    {
        Host?.PlayAppSound(SoundType.Hide);
    }

    private async void ActionTestButton_OnClick(SplitButton sender, SplitButtonClickEventArgs e)
    {
        if (!TestResultsBox.IsLoaded || TreeSelectedAction is null) return;
        TestResultsBox.Text = await TreeSelectedAction.Invoke(null);
    }

    private async void RemoveAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TestResultsBox.IsLoaded || TreeSelectedAction is null) return;
        DataParent.VrInput.RegisteredActions.Actions.Remove(TreeSelectedAction);
        DataParent.VrInput.SaveSettings();

        TreeSelectedAction = null;
        ActionRemoveFlyout.Hide();

        OnPropertyChanged();
        await Tree_LaunchTransition();
    }

    private async Task Tree_LaunchTransition()
    {
        // Action stuff reload animation
        try
        {
            // Remove the only one child of our outer main content grid
            // (What a bestiality it is to do that!!1)
            OuterGrid.Children.Remove(PreviewGrid);
            PreviewGrid.Transitions.Add(
                new EntranceThemeTransition { IsStaggeringEnabled = false });

            // Sleep peacefully pretending that noting happened
            await Task.Delay(10);

            // Re-add the child for it to play our funky transition
            // (Though it's not the same as before...)
            OuterGrid.Children.Add(PreviewGrid);

            // Remove the transition
            await Task.Delay(100);
            PreviewGrid.Transitions.Clear();
        }
        catch (Exception e)
        {
            Host?.Log(e);
        }
    }

    private async void ActionsListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView view || _listViewChangeBlock) return;
        if (e.AddedItems.FirstOrDefault() is not InputAction action)
        {
            await Tree_LaunchTransition();
            Host?.PlayAppSound(SoundType.Focus);
            return; // Give up now...
        }

        var shouldAnimate = TreeSelectedAction != action;
        TreeSelectedAction = action;
        IsAddingNewAction = false;
        OnPropertyChanged();

        if (!shouldAnimate) return;
        await Tree_LaunchTransition();
        Host?.PlayAppSound(SoundType.Invoke);
    }

    private async void NewActionItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ActionsListView.IsLoaded) return;

        ActionsListView.SelectionMode = ListViewSelectionMode.None;
        ActionsListView.SelectionMode = ListViewSelectionMode.Single;

        TreeSelectedAction = new InputAction(
            $"/actions/default/in/{Guid.NewGuid().ToString().ToUpper()}",
            "boolean", "optional");

        NewActionName = string.Empty;
        IsAddingNewAction = true;

        Host?.PlayAppSound(SoundType.Invoke);
        OnPropertyChanged();
        await Tree_LaunchTransition();
    }

    private void ReManifestButton_OnClick(object sender, RoutedEventArgs e)
    {
        switch (InstallVrApplicationManifest())
        {
            // Not found failure
            case -2:
            {
                ActionFailedFlyout.Content = new TextBlock
                {
                    FontWeight = FontWeights.SemiBold,
                    Text = Host.RequestLocalizedString("/SettingsPage/ReManifest/Error/NotFound")
                };

                ActionFailedFlyout.ShowAt(ReManifestButton);
                break;
            }

            // SteamVR failure
            case 1:
            {
                ActionFailedFlyout.Content = new TextBlock
                {
                    FontWeight = FontWeights.SemiBold,
                    Text = Host.RequestLocalizedString("/SettingsPage/ReManifest/Error/Other")
                };

                ActionFailedFlyout.ShowAt(ReManifestButton);
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
            // Check whether Amethyst is installed as a package
            if (PackageUtils.IsAmethystPackaged)
            {
                // Copy all driver files to Amethyst's local data folder
                Directory.CreateDirectory(Path.Join(ApplicationData.Current.LocalFolder.Path, DataParent.DriverFolderName));

                // Copy the manifest
                new FileInfo(Path.Join(
                        Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.FullName, "Amethyst.vrmanifest"))
                    .CopyTo(Path.Join(ApplicationData.Current.LocalFolder.Path, DataParent.DriverFolderName, "Amethyst.vrmanifest"), true);

                // Copy the icon
                var icon = new FileInfo(Path.Join(
                    Directory.GetParent(Environment.ProcessPath!)!.FullName, "Assets", "ktvr.png"));

                if (icon.Exists)
                    icon.CopyTo(Path.Join(ApplicationData.Current.LocalFolder.Path, DataParent.DriverFolderName, "ktvr.png"), true);

                // Assume it's done now and get the path
                var copiedManifestPath =
                    Path.Join(ApplicationData.Current.LocalFolder.Path, DataParent.DriverFolderName, "Amethyst.vrmanifest");

                // If there's none (still), cry about it and abort
                if (string.IsNullOrEmpty(copiedManifestPath) || !File.Exists(copiedManifestPath))
                {
                    // Hide the "working" progress bar
                    ReRegisterButtonBar.Opacity = 0.0;

                    Host?.Log($"Copied driver not present at expectant path of: {copiedManifestPath}");
                    Host?.Log($"Amethyst vr manifest ({copiedManifestPath}) not found!", LogSeverity.Warning);
                    return -2;
                }
            }

            if (OpenVR.Applications.IsApplicationInstalled("K2VR.Amethyst"))
            {
                Host.Log("Amethyst manifest is already installed, removing...");

                OpenVR.Applications.RemoveApplicationManifest(
                    "C:/Program Files/ModifiableWindowsApps/K2VRTeam.Amethyst.App/Plugins/plugin_OpenVR/Amethyst.vrmanifest");
                OpenVR.Applications.RemoveApplicationManifest("../../Plugins/plugin_OpenVR/Amethyst.vrmanifest");
            }

            // Compose the manifest path depending on where our plugin is
            var manifestPath = PackageUtils.IsAmethystPackaged
                ? Path.Join(ApplicationData.Current.LocalFolder.Path, DataParent.DriverFolderName, "Amethyst.vrmanifest")
                : Path.Join(Directory.GetParent(
                    Assembly.GetAssembly(GetType())!.Location)?.FullName, "Amethyst.vrmanifest");

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
                    manifestJson.applications.FirstOrDefault()!.launch_type =
                        Package.Current is not null ? "url" : "binary"; // Modify the manifest
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

    public async void ReRegisterButton_OnClick(SplitButton sender, SplitButtonClickEventArgs args)
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
                        if (File.Exists(Path.Combine(Directory.GetParent(localDriverManifest).ToString(), "bin",
                                "win64",
                                "driver_Amethyst.dll")))
                        {
                            // We've found it! Now cache it and break free
                            localAmethystDriverPath = Directory.GetParent(localDriverManifest).ToString();
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
                // Hide the "working" progress bar
                ReRegisterButtonBar.Opacity = 0.0;

                await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/DriverNotFound"), "", "");
                return; // Hide and exit the handler
            }
        }

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

        // Check whether Amethyst is installed as a package
        if (PackageUtils.IsAmethystPackaged)
        {
            // Copy all driver files to Amethyst's local data folder
            new DirectoryInfo(Path.Join(Directory.GetParent(
                    Assembly.GetExecutingAssembly().Location)!.FullName, "Driver", DataParent.DriverFolderName))
                .CopyToFolder((await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                    DataParent.DriverFolderName, CreationCollisionOption.OpenIfExists)).Path);

            // Assume it's done now and get the path
            localAmethystDriverPath = Path.Join(PackageUtils.GetAmethystAppDataPath(), DataParent.DriverFolderName);

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
            if (externalDriver == localAmethystDriverPath)
            {
                isLocalAmethystDriverRegistered = true;
                continue; // Don't report it
            }

            isAmethystDriverPresent = !externalDriver.StartsWith(ApplicationData.Current.LocalFolder.Path);
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
                                 amethystDriverPath != localAmethystDriverPath))
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
            try // Try-Catch it
            {
                // Register the local Amethyst Driver via OpenVRPaths
                openVrPaths.external_drivers.Add(localAmethystDriverPath);
                openVrPaths.Write(); // Save it

                // If failed, cry about it and abort
                var openVrPathsCheck = OpenVrPaths.Read();
                if (!openVrPathsCheck.external_drivers.Contains(localAmethystDriverPath))
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

        /* 5 */

        // Try-Catch it
        try
        {
            // Read the vr settings
            var steamVrSettings = JsonObject.Parse(await File.ReadAllTextAsync(resultPaths.Path.VrSettingsPath));

            // Enable & unblock the Amethyst Driver
            steamVrSettings.Remove($"driver_{SteamVR.Instance.DriverFolderName}");
            steamVrSettings.Add($"driver_{SteamVR.Instance.DriverFolderName}",
                new JsonObject
                {
                    new KeyValuePair<string, IJsonValue>("enable", JsonValue.CreateBooleanValue(true)),
                    new KeyValuePair<string, IJsonValue>("blocked_by_safe_mode", JsonValue.CreateBooleanValue(false))
                });

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

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged(string propertyName = null)
    {
        _listViewChangeBlock = true;
        var itemBackup = ActionsListView.SelectedItem;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        if (ActionsListView.Items.Contains(itemBackup))
            ActionsListView.SelectedItem = itemBackup;
        _listViewChangeBlock = false;
    }

    private async void AddNewAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (!((sender as Button)?.IsLoaded ?? false)) return;

        DataParent.VrInput.RegisteredActions.Actions.Add(TreeSelectedAction);
        TreeSelectedAction.NameLocalized = NewActionName;
        DataParent.VrInput.SaveSettings();
        DataParent.VrInput.InitInputActions();

        IsAddingNewAction = false;
        Host?.PlayAppSound(SoundType.Invoke);

        ActionsListView.ItemContainerTransitions.Clear();
        ReloadActions();
        ActionsListView.ItemContainerTransitions = [];

        if (ActionsListView.Items.Any())
            ActionsListView.SelectedItem = ActionsListView.Items.Last();

        await Tree_LaunchTransition();
    }
}