// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.System;
using Amethyst.Plugins.Contract;
using MessageContract;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Newtonsoft.Json;
using plugin_OpenVR.Utils;
using StreamJsonRpc;
using Valve.VR;
using System.Web;
using Windows.Storage;

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace plugin_OpenVR;

[Export(typeof(IServiceEndpoint))]
[ExportMetadata("Name", "SteamVR")]
[ExportMetadata("Guid", "K2VRTEAM-AME2-APII-SNDP-SENDPTOPENVR")]
[ExportMetadata("Publisher", "K2VR Team")]
[ExportMetadata("Version", "1.0.0.1")]
[ExportMetadata("Website", "https://github.com/KinectToVR/plugin_OpenVR")]
public class SteamVR : IServiceEndpoint
{
    private NamedPipeClientStream _clientNamedPipeStream;
    private JsonRpc _driverJsonRpcHandler;

    private EvrInput.SteamEvrInput _evrInput;
    private uint _vrNotificationId;

    private ulong _vrOverlayHandle = OpenVR.k_ulOverlayHandleInvalid;
    public static bool Initialized { get; private set; }
    private static object InitLock { get; } = new();

    private bool PluginLoaded { get; set; }
    private Page InterfaceRoot { get; set; }
    private Button ReManifestButton { get; set; }
    private Button ReRegisterButton { get; set; }
    private Flyout ActionFailedFlyout { get; set; }

    private Vector3 VrPlayspaceTranslation =>
        OpenVR.System.GetRawZeroPoseToStandingAbsoluteTrackingPose().GetPosition();

    private Quaternion VrPlayspaceOrientationQuaternion =>
        OpenVR.System.GetRawZeroPoseToStandingAbsoluteTrackingPose().GetOrientation();

    private Exception ServerDriverException { get; set; }
    private bool ServerDriverPresent => ServiceStatus == 0;

    [Import(typeof(IAmethystHost))] private IAmethystHost Host { get; set; }

    public bool CanAutoStartAmethyst => true;

    public bool IsSettingsDaemonSupported => true;

    public object SettingsInterfaceRoot => InterfaceRoot;

    public int ServiceStatus { get; private set; }

    [DefaultValue("Not Defined\nE_NOT_DEFINED\nStatus message not defined!")]
    public string ServiceStatusString => PluginLoaded
        ? ServiceStatus switch
        {
            0 => Host.RequestLocalizedString("/ServerStatuses/Success")
                .Replace("{0}", ServiceStatus.ToString()),

            1 when VrHelper.IsOpenVrElevated() =>
                Host.RequestLocalizedString("/ServerStatuses/OpenVRElevatedError")
                    .Replace("{0}", "0x80070005"),

            1 when VrHelper.IsCurrentProcessElevated() =>
                Host.RequestLocalizedString("/ServerStatuses/AmethystElevatedError")
                    .Replace("{0}", "0x80080017"),

            1 => Host.RequestLocalizedString("/ServerStatuses/OpenVRError")
                .Replace("{0}", ServiceStatus.ToString()),

            2 => Host.RequestLocalizedString("/ServerStatuses/IVRInputError")
                .Replace("{0}", ServiceStatus.ToString()),

            -1 => Host.RequestLocalizedString("/ServerStatuses/ConnectionError")
                .Replace("{0}", ServiceStatus.ToString()),

            -10 => Host.RequestLocalizedString("/ServerStatuses/Exception")
                .Replace("{0}", ServiceStatus.ToString()
                    .Replace("{1}", ServerDriverException.Message)),

            -2 => Host.RequestLocalizedString("/ServerStatuses/RPCChannelFailure")
                .Replace("{0}", ServiceStatus.ToString()),

            10 => Host.RequestLocalizedString("/ServerStatuses/ServerFailure")
                .Replace("{0}", ServiceStatus.ToString()),

            _ => Host.RequestLocalizedString("/ServerStatuses/WTF")
        }
        : $"Undefined: {ServiceStatus}\nE_UNDEFINED\nSomething weird has happened, though we can't tell what.";

    public Uri ErrorDocsUri => new(ServiceStatus switch
    {
        -10 => $"https://docs.k2vr.tech/{Host?.DocsLanguageCode ?? "en"}/app/steamvr-driver-codes/#2",
        -1 => $"https://docs.k2vr.tech/{Host?.DocsLanguageCode ?? "en"}/app/steamvr-driver-codes/#3",
        _ => $"https://docs.k2vr.tech/{Host?.DocsLanguageCode ?? "en"}/app/steamvr-driver-codes/#6"
    });

    public SortedSet<TrackerType> AdditionalSupportedTrackerTypes =>
        new()
        {
            TrackerType.TrackerHanded,
            // TrackerType.TrackerLeftFoot, // Already OK
            // TrackerType.TrackerRightFoot, // Already OK
            TrackerType.TrackerLeftShoulder,
            TrackerType.TrackerRightShoulder,
            TrackerType.TrackerLeftElbow,
            TrackerType.TrackerRightElbow,
            TrackerType.TrackerLeftKnee,
            TrackerType.TrackerRightKnee,
            // TrackerType.TrackerWaist, // Already OK
            TrackerType.TrackerChest,
            TrackerType.TrackerCamera,
            TrackerType.TrackerKeyboard
        };

    public bool IsRestartOnChangesNeeded => true;

    public InputActions ControllerInputActions { get; set; } = new()
    {
        CalibrationConfirmed = (_, _) => { },
        CalibrationModeChanged = (_, _) => { },
        SkeletonFlipToggled = (_, _) => { },
        TrackingFreezeToggled = (_, _) => { }
    };

    public bool AutoStartAmethyst
    {
        get => OpenVR.Applications?.GetApplicationAutoLaunch("K2VR.Amethyst") ?? false;
        set
        {
            if (!Initialized || OpenVR.Applications is null) return; // Sanity check
            InstallVrApplicationManifest(); // Just in case
            var appError = OpenVR.Applications.SetApplicationAutoLaunch("K2VR.Amethyst", value);

            if (appError != EVRApplicationError.None)
                Host.Log("Amethyst manifest not installed! Error: " +
                         $"{OpenVR.Applications.GetApplicationsErrorNameFromEnum(appError)}", LogSeverity.Warning);
        }
    }

    public bool AutoCloseAmethyst
    {
        get => _vrOverlayHandle != OpenVR.k_ulOverlayHandleInvalid;
        set { } // ignored, closes always
    }

    public bool IsAmethystVisible
    {
        get
        {
            if (!Initialized || OpenVR.System is null ||
                OpenVR.Overlay is null) return true; // Sanity check

            // Check if we're running on null
            StringBuilder systemStringBuilder = new(1024);
            var propertyError = ETrackedPropertyError.TrackedProp_Success;
            OpenVR.System.GetStringTrackedDeviceProperty(
                OpenVR.k_unTrackedDeviceIndex_Hmd, ETrackedDeviceProperty.Prop_TrackingSystemName_String,
                systemStringBuilder, 1024, ref propertyError);

            // Just return true for debug reasons
            if (systemStringBuilder.ToString().Contains("null") ||
                propertyError != ETrackedPropertyError.TrackedProp_Success)
                return true;

            // Also check if we're not idle / standby
            var status = OpenVR.System.GetTrackedDeviceActivityLevel(OpenVR.k_unTrackedDeviceIndex_Hmd);
            if (status != EDeviceActivityLevel.k_EDeviceActivityLevel_UserInteraction &&
                status != EDeviceActivityLevel.k_EDeviceActivityLevel_UserInteraction_Timeout)
                return false; // Standby - hide

            // Check if the dashboard is open
            return OpenVR.Overlay?.IsDashboardVisible() ?? false;
        }
    }

    public string TrackingSystemName
    {
        get
        {
            if (!Initialized || OpenVR.System is null) return null; // Sanity check
            var controllerModel = new StringBuilder(1024);
            var error = ETrackedPropertyError.TrackedProp_Success;

            OpenVR.System.GetStringTrackedDeviceProperty(
                OpenVR.System.GetTrackedDeviceIndexForControllerRole(
                    ETrackedControllerRole.LeftHand),
                ETrackedDeviceProperty.Prop_ModelNumber_String,
                controllerModel, 1024, ref error);

            // Actually, controller system name
            return controllerModel.ToString();
        }
    }

    public void OnLoad()
    {
        _evrInput ??= new EvrInput.SteamEvrInput(Host);

        ReManifestButton = new Button
        {
            Content = new TextBlock
            {
                Text = Host.RequestLocalizedString("/SettingsPage/Buttons/ReManifest"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 16, FontWeight = FontWeights.SemiBold
            },
            Height = 40, HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness { Right = 6 }
        };

        ReRegisterButton = new Button
        {
            Content = new TextBlock
            {
                Text = Host.RequestLocalizedString("/SettingsPage/Buttons/ReRegister"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 16, FontWeight = FontWeights.SemiBold
            },
            Height = 40, HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness { Left = 6 }
        };

        Grid.SetColumn(ReManifestButton, 0);
        ReManifestButton.Click += (_, _) =>
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

                    ActionFailedFlyout.ShowAt(ReManifestButton, new FlyoutShowOptions
                    {
                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                    });
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

                    ActionFailedFlyout.ShowAt(ReManifestButton, new FlyoutShowOptions
                    {
                        Placement = FlyoutPlacementMode.BottomEdgeAlignedRight
                    });
                    break;
                }
            }

            // Play a sound
            Host?.PlayAppSound(SoundType.Invoke);
        };

        Grid.SetColumn(ReRegisterButton, 1);
        ReRegisterButton.Click += ReRegisterButton_Click;

        InterfaceRoot = new Page
        {
            Content = new Grid
            {
                Children = { ReManifestButton, ReRegisterButton },
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            }
        };

        ActionFailedFlyout = new Flyout();
        ActionFailedFlyout.Opening += (_, _) => Host?.PlayAppSound(SoundType.Show);
        ActionFailedFlyout.Closing += (_, _) => Host?.PlayAppSound(SoundType.Hide);

        PluginLoaded = true;
    }

    public int Initialize()
    {
        // Reset the status
        ServiceStatus = 0;

        // Check if Amethyst is running as admin
        // Check if OpenVR is running as admin
        // Initialize OpenVR if we're ready to go
        if (VrHelper.IsCurrentProcessElevated() !=
            VrHelper.IsOpenVrElevated() || !OpenVrStartup())
        {
            ServiceStatus = 1;
            return 1;
        }

        // Install the manifest
        InstallVrApplicationManifest();

        // Update bindings
        UpdateBindingTexts();

        // Startup input actions
        var serviceStatus = 0;
        if (!EvrActionsStartup()) serviceStatus = 2;

        // Connect to the server driver
        Task.Run(K2ServerDriverRefreshAsync).Wait();

        // Return the the binding error if the driver is fine
        return ServiceStatus == 0 ? serviceStatus : ServiceStatus;
    }

    public void Heartbeat()
    {
        try
        {
            lock (InitLock)
            {
                UpdateInputBindings();
                ParseVrEvents();
            }
        }
        catch (Exception e)
        {
            Host?.Log("Exception processing a heart beat call! " +
                      $"Message: {e.Message}", LogSeverity.Error);
        }
    }

    public void Shutdown()
    {
        lock (InitLock)
        lock (Host.UpdateThreadLock)
        {
            Initialized = false; // vrClient dll unloaded
            OpenVR.Shutdown(); // Shutdown OpenVR

            ServiceStatus = 1; // Update VR status
            Task.Run(K2ServerDriverRefreshAsync).Wait();
        }
    }

    public void DisplayToast((string Title, string Text) message)
    {
        if (_vrOverlayHandle == OpenVR.k_ulOverlayHandleInvalid ||
            string.IsNullOrEmpty(message.Title) ||
            string.IsNullOrEmpty(message.Text)) return;

        // Hide the current notification (if being shown)
        if (_vrNotificationId != 0) // If valid
            OpenVR.Notifications?.RemoveNotification(_vrNotificationId);

        // Empty image data
        var notificationBitmap = new NotificationBitmap_t();

        // null is the icon/image texture
        OpenVR.Notifications?.CreateNotification(
            _vrOverlayHandle, 0, EVRNotificationType.Transient,
            message.Title + '\n' + message.Text, EVRNotificationStyle.Application,
            ref notificationBitmap, ref _vrNotificationId);
    }

    public bool? RequestServiceRestart(string reason, bool wantReply = false)
    {
        try
        {
            if (!Initialized || OpenVR.System is null) return true; // Sanity check

            // Auto-returns null if the service is null
            var restartTask = _driverJsonRpcHandler.InvokeAsync<bool>(nameof(IRpcServer.RequestVrRestart), reason);
            var result = restartTask.Result;

            restartTask.RunSynchronously();
            return result; // Wait and return
        }
        catch (Exception)
        {
            return wantReply ? false : null;
        }
    }

    public (Vector3 Position, Quaternion Orientation)? HeadsetPose
    {
        get
        {
            if (!Initialized || OpenVR.System is null) return (Vector3.Zero, Quaternion.Identity); // Sanity check

            // Capture RAW HMD pose
            var devicePose = new TrackedDevicePose_t[1]; // HMD only
            OpenVR.System.GetDeviceToAbsoluteTrackingPose(
                ETrackingUniverseOrigin.TrackingUniverseStanding, 0, devicePose);

            // Assert that HMD is at index 0
            if (OpenVR.System.GetTrackedDeviceClass(0) != ETrackedDeviceClass.HMD)
                return (Vector3.Zero, Quaternion.Identity); // Failed

            (Vector3 Position, Quaternion Orientation) raw =
                (devicePose[0].mDeviceToAbsoluteTracking.GetPosition(),
                    devicePose[0].mDeviceToAbsoluteTracking.GetOrientation());

            return (Vector3.Transform(raw.Position - VrPlayspaceTranslation,
                    Quaternion.Inverse(VrPlayspaceOrientationQuaternion)),
                Quaternion.Inverse(VrPlayspaceOrientationQuaternion) * raw.Orientation);
        }
    }

    public TrackerBase GetTrackerPose(string contains, bool canBeFromAmethyst = true)
    {
        if (!Initialized || OpenVR.System is null) return null; // Sanity check

        (bool Found, uint Index) FindVrTracker(string role, bool canBeAme = true)
        {
            // Loop through all devices
            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                StringBuilder roleStringBuilder = new(1024);
                var roleError = ETrackedPropertyError.TrackedProp_Success;
                OpenVR.System.GetStringTrackedDeviceProperty(
                    i, ETrackedDeviceProperty.Prop_ControllerType_String,
                    roleStringBuilder, (uint)roleStringBuilder.Capacity, ref roleError);

                if (roleStringBuilder.Length <= 0)
                    continue; // Don't waste our time

                // If we've actually found the one
                if (roleStringBuilder.ToString().IndexOf(role, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var status = OpenVR.System.GetTrackedDeviceActivityLevel(i);
                if (status != EDeviceActivityLevel.k_EDeviceActivityLevel_UserInteraction &&
                    status != EDeviceActivityLevel.k_EDeviceActivityLevel_UserInteraction_Timeout)
                    continue;

                StringBuilder serialStringBuilder = new(1024);
                var serialError = ETrackedPropertyError.TrackedProp_Success;
                OpenVR.System.GetStringTrackedDeviceProperty(i, ETrackedDeviceProperty.Prop_SerialNumber_String,
                    serialStringBuilder, (uint)serialStringBuilder.Capacity, ref serialError);

                // Check if it's not ame, return what we've got
                if (!(!canBeAme && serialStringBuilder.ToString().Contains("AME-"))) return (true, i);
            }

            // We've failed if the loop's finished
            return (false, OpenVR.k_unTrackedDeviceIndexInvalid);
        }

        var devicePose = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        OpenVR.System.GetDeviceToAbsoluteTrackingPose(
            ETrackingUniverseOrigin.TrackingUniverseStanding, 0, devicePose);

        var trackerPair = FindVrTracker(contains, false);
        if (!trackerPair.Found) return null;

        // Extract pose from the returns
        // We don't care if it's invalid by any chance
        var waistPose = devicePose[trackerPair.Index];

        // Get pos & rot
        return new TrackerBase
        {
            Position = Vector3.Transform(waistPose
                    .mDeviceToAbsoluteTracking.GetPosition() - VrPlayspaceTranslation,
                Quaternion.Inverse(VrPlayspaceOrientationQuaternion)),

            Orientation = Quaternion.Inverse(VrPlayspaceOrientationQuaternion) *
                          waistPose.mDeviceToAbsoluteTracking.GetOrientation()
        };
    }

    public async Task<IEnumerable<(TrackerBase Tracker, bool Success)>> SetTrackerStates(
        IEnumerable<TrackerBase> trackerBases, bool wantReply = true)
    {
        try
        {
            // Driver client sanity check: return empty or null if not valid
            if (!Initialized || OpenVR.System is null || _driverJsonRpcHandler is null || ServiceStatus != 0)
                return wantReply ? new List<(TrackerBase Tracker, bool Success)>() : null;

            return (await _driverJsonRpcHandler.InvokeAsync<IEnumerable<(TrackerType Tracker, bool Success)>?>(
                    nameof(IRpcServer.SetTrackerStateList), trackerBases.ToList(), wantReply))?
                .Select(x => (new TrackerBase { Role = x.Tracker }, x.Success));
        }
        catch (Exception e)
        {
            Host?.Log($"Failed to update one or more trackers, exception: {e.Message}");
            return wantReply ? new List<(TrackerBase Tracker, bool Success)> { (null, false) } : null;
        }
    }

    public async Task<IEnumerable<(TrackerBase Tracker, bool Success)>> UpdateTrackerPoses(
        IEnumerable<TrackerBase> trackerBases, bool wantReply = true)
    {
        try
        {
            // Driver client sanity check: return empty or null if not valid
            if (!Initialized || OpenVR.System is null || _driverJsonRpcHandler is null || ServiceStatus != 0)
                return wantReply ? new List<(TrackerBase Tracker, bool Success)>() : null;

            await _driverJsonRpcHandler.InvokeAsync<IEnumerable<(TrackerType Tracker, bool Success)>?>(
                nameof(IRpcServer.UpdateTrackerList), trackerBases.ToList(), wantReply);

            // Discard the result to save resources, as it's not even used anywhere
            return wantReply ? new List<(TrackerBase Tracker, bool Success)>() : null;
        }
        catch (Exception e)
        {
            Host?.Log($"Failed to update one or more trackers, exception: {e.Message}");
            return wantReply ? new List<(TrackerBase Tracker, bool Success)>() : null;
        }
    }

    public async Task<(int Status, string StatusMessage, long PingTime)> TestConnection()
    {
        try
        {
            // Update bindings
            UpdateBindingTexts();

            // Refresh the driver, just in case
            await K2ServerDriverRefreshAsync();

            // Driver client sanity check: return empty or null if not valid
            if (!Initialized || OpenVR.System is null || _driverJsonRpcHandler is null ||
                ServiceStatus != 0) return (-1, "SERVICE_INVALID", 0);

            // Grab the current time and send the message
            var messageSendTimeStopwatch = new Stopwatch();

            messageSendTimeStopwatch.Start();
            await _driverJsonRpcHandler.InvokeAsync<DateTime>(nameof(IRpcServer.PingDriverService));
            messageSendTimeStopwatch.Stop();

            // Return tuple with response and elapsed time
            return (0, "OK", messageSendTimeStopwatch.ElapsedTicks);
        }
        catch (Exception e)
        {
            ServiceStatus = -10;
            ServerDriverException = e;
            return (-1, $"EXCEPTION {e.Message}", 0);
        }
    }

    private async void ReRegisterButton_Click(object o, RoutedEventArgs routedEventArgs)
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
                await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/DriverNotFound"), "", "");
                return;
            }
        }

        /* 2 */

        // Force exit (kill) SteamVR
        if (Process.GetProcesses().FirstOrDefault(proc => proc.ProcessName is "vrserver" or "vrmonitor") != null)
        {
            // Check for privilege mismatches
            if (VrHelper.IsOpenVrElevated() && !VrHelper.IsCurrentProcessElevated())
            {
                await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/Elevation"), "", "");
                return; // Suicide was always an option
            }

            // Finally kill
            if (await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/KillSteamVR/Content"),
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/KillSteamVR/PrimaryButton"),
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/KillSteamVR/SecondaryButton")))
                await Task.Factory.StartNew(() =>
                {
                    Shutdown(); // Exit not to be killed
                    Host.RefreshStatusInterface();
                    return helper.CloseSteamVr();
                });
            else
                return;
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
                await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/DriverNotFound"), "", "");
                return;
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
            // Critical, cry about it
            await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/FatalRemoveException_K2EX"), "", "");
            return;
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

            isAmethystDriverPresent = true;
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
            if (isAmethystDriverPresent || resultPaths.Exists.CopiedDriverExists)
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
            // Critical, cry about it
            await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/FatalRemoveException"), "", "");
            return;
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
                    await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                        Host?.RequestLocalizedString("/CrashHandler/ReRegister/OpenVRPathsWriteError"), "", "");
                    return;
                }
            }
            catch (Exception)
            {
                // Critical, cry about it
                await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/FatalRegisterException"), "", "");
                return;
            }

        /* 5 */

        // Try-Catch it
        try
        {
            // Read the vr settings
            var steamVrSettings = JsonObject.Parse(await File.ReadAllTextAsync(resultPaths.Path.VrSettingsPath));

            // Enable & unblock the Amethyst Driver
            steamVrSettings.Remove("driver_Amethyst");
            steamVrSettings.Add("driver_Amethyst",
                new JsonObject
                {
                    new("enable", JsonValue.CreateBooleanValue(true)),
                    new("blocked_by_safe_mode", JsonValue.CreateBooleanValue(false))
                });

            await File.WriteAllTextAsync(resultPaths.Path.VrSettingsPath, steamVrSettings.ToString());
        }
        catch (Exception)
        {
            // Not critical
        }

        // Winning it!
        await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
            Host?.RequestLocalizedString("/CrashHandler/ReRegister/Finished"), "", "");
    }

    #region Amethyst VRDriver Methods

    private async Task<int> InitAmethystServerAsync(string target = "jhbDkiHugI&O(*TYOLsUIhFli;h")
    {
        try
        {
            Host?.Log("Creating the server handle...");
            _clientNamedPipeStream = new NamedPipeClientStream(".",
                target, PipeDirection.InOut, PipeOptions.Asynchronous);

            Host?.Log("Connecting to the server...");
            await _clientNamedPipeStream.ConnectAsync(1000);

            Host?.Log("Setting up serialization resolvers...");
            var resolver = CompositeResolver.Create(
                NumericsResolver.Instance,
                StandardResolver.Instance
            );

            Host?.Log("Preparing the formatter...");
            var formatter = new MessagePackFormatter();
            formatter.SetMessagePackSerializerOptions(
                MessagePackSerializerOptions.Standard.WithResolver(resolver));

            Host?.Log("Instantiating the rpc handler...");
            _driverJsonRpcHandler = new JsonRpc(new LengthHeaderMessageHandler(
                _clientNamedPipeStream, _clientNamedPipeStream, formatter))
            {
                TraceSource = new TraceSource("Client", SourceLevels.Verbose)
            };

            _driverJsonRpcHandler.TraceSource.Listeners.Add(new ConsoleTraceListener());

            Host?.Log("Starting the listener...");
            _driverJsonRpcHandler.StartListening();
        }
        catch (TimeoutException e)
        {
            Host?.Log(e.ToString(), LogSeverity.Error);
            ServerDriverException = e; // Backup the exception
            return -1;
        }
        catch (Exception e)
        {
            Host?.Log(e.ToString(), LogSeverity.Error);
            ServerDriverException = e; // Backup the exception
            return -10;
        }

        return 0;
    }

    private async Task<int> CheckK2ServerStatusAsync()
    {
        // Don't check if OpenVR failed
        if (ServiceStatus == 1) return 1;

        try
        {
            /* Initialize the port */
            Host.Log("Initializing the server IPC...");
            var initCode = await InitAmethystServerAsync();

            Host.Log($"Server IPC initialization {(initCode == 0 ? "succeed" : "failed")}, exit code: {initCode}",
                initCode == 0 ? LogSeverity.Info : LogSeverity.Error);

            try
            {
                // Driver client sanity check: return empty or null if not valid
                if (!Initialized || OpenVR.System is null)
                    return 1;

                if (initCode != 0)
                    return initCode;

                if (_driverJsonRpcHandler is null)
                    return -2;

                // Grab the current time and send the message
                await _driverJsonRpcHandler.InvokeAsync<DateTime>(
                    nameof(IRpcServer.PingDriverService));

                return 0; // Everything should be fine
            }
            catch (Exception e)
            {
                ServerDriverException = e;
                return -10;
            }
        }
        catch (Exception e)
        {
            Host.Log("Server status check failed! " +
                     $"Exception: {e.Message}", LogSeverity.Warning);

            return -10;
        }

        /*
         * codes:
            all ok: 0
            
            server could not be reached - timeout: -1
            server could not be reached - exception: -10
            
            server handler was invalid - null: -2
            
            fatal run-time failure: 10
         */
    }

    private async Task K2ServerDriverRefreshAsync()
    {
        ServiceStatus = await CheckK2ServerStatusAsync();

        // Request a quick status refresh
        Host?.RefreshStatusInterface();
    }

    #endregion

    #region OpenVR Interfacing Methods

    private bool OpenVrStartup()
    {
        // Only re-init VR if needed
        if (OpenVR.System is null)
        {
            Host.Log("Attempting connection to VRSystem... ");

            try
            {
                Host.Log("Creating a cancellation token...");
                using var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(7));

                Host.Log("Waiting for the VR System to initialize...");
                var eError = EVRInitError.None;

                OpenVR.Init(ref eError, EVRApplicationType.VRApplication_Overlay);
                Initialized = true; // vrClient dll loaded

                Host.Log("The VRSystem finished initializing...");
                if (eError != EVRInitError.None)
                {
                    Host.Log($"IVRSystem could not be initialized: EVRInitError Code {eError}", LogSeverity.Error);
                    return false; // Catastrophic failure!
                }
            }
            catch (Exception e)
            {
                Host.Log($"The VR System failed to initialize ({e.Message}), giving up!", LogSeverity.Error);
                return false; // Took too long to initialize, abort!
            }
        }

        // Re-check
        if (OpenVR.System is null) return false;

        // We're good to go!
        Host.Log("Looks like the VR System is ready to go!");

        // Initialize the overlay
        OpenVR.Overlay?.DestroyOverlay(_vrOverlayHandle); // Destroy the overlay in case it somehow exists
        OpenVR.Overlay?.CreateOverlay("k2vr.amethyst.desktop", "Amethyst", ref _vrOverlayHandle);

        Host.Log($"VR Playspace translation: \n{VrPlayspaceTranslation}");
        Host.Log($"VR Playspace orientation: \n{VrPlayspaceOrientationQuaternion}");
        return true; // OK
    }

    private int InstallVrApplicationManifest()
    {
        if (!Initialized || OpenVR.Applications is null) return 0; // Sanity check
        if (OpenVR.Applications.IsApplicationInstalled("K2VR.Amethyst"))
        {
            Host.Log("Amethyst manifest is already installed, removing...");

            OpenVR.Applications.RemoveApplicationManifest(
                "C:/Program Files/ModifiableWindowsApps/K2VRTeam.Amethyst.App/Plugins/plugin_OpenVR/Amethyst.vrmanifest");
            OpenVR.Applications.RemoveApplicationManifest("../../Plugins/plugin_OpenVR/Amethyst.vrmanifest");
        }

        // Compose the manifest path depending on where our plugin is
        var manifestPath = Path.Join(Directory.GetParent(Assembly
            .GetAssembly(GetType())!.Location)?.FullName, "Amethyst.vrmanifest");

        try
        {
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

        Host?.Log($"Amethyst vr manifest ({manifestPath}) not found!", LogSeverity.Warning);
        return -2;
    }

    private bool EvrActionsStartup()
    {
        Host.Log("Attempting to set up EVR Input Actions...");

        if (!_evrInput.InitInputActions())
        {
            Host.Log("Could not set up Input Actions. Please check the upper log for further information.",
                LogSeverity.Error);
            DisplayToast(("EVR Input Actions Init Failure!",
                "Couldn't set up Input Actions. Please check the log file for further information."));
            return false;
        }

        Host.Log("EVR Input Actions set up OK");
        return true;
    }

    private void UpdateInputBindings()
    {
        // Here, update EVR Input actions
        if (!Initialized || OpenVR.System is null) return;

        // Backup the current (OLD) data
        var bFreezeState = _evrInput.TrackerFreezeActionData.bState;
        var bFlipToggleState = _evrInput.TrackerFlipToggleData.bState;

        // Update all input actions
        if (!_evrInput.UpdateActionStates())
            Host.Log("Could not update EVR Input Actions. Please check logs for further information",
                LogSeverity.Error);

        // Update the Tracking Freeze : toggle
        // Only if the state has changed from 1 to 0: button was clicked
        if (!_evrInput.TrackerFreezeActionData.bState && bFreezeState)
        {
            Host.Log("[Input Actions] Input: Tracking freeze toggled");
            ControllerInputActions.TrackingFreezeToggled?.Invoke(this, EventArgs.Empty);
        }

        // Update the Flip Toggle : toggle
        // Only if the state has changed from 1 to 0: button was clicked
        if (!_evrInput.TrackerFlipToggleData.bState && bFlipToggleState)
        {
            Host.Log("[Input Actions] Input: Flip toggled");
            ControllerInputActions.SkeletonFlipToggled?.Invoke(this, EventArgs.Empty);
        }

        // Update the Calibration:Confirm : one-time switch
        // Only one-way switch this time, reset at calibration's end
        if (_evrInput.ConfirmAndSaveActionData.bState)
            ControllerInputActions.CalibrationConfirmed?.Invoke(this, EventArgs.Empty);

        // Update the Calibration:ModeSwap : one-time switch
        // Only if the state has changed from 1 to 0: chord was done
        if (_evrInput.ModeSwapActionData.bState)
            ControllerInputActions.CalibrationModeChanged?.Invoke(this, EventArgs.Empty);

        // Update the Calibration:FineTune : held switch
        var posMultiplexer = _evrInput.FineTuneActionData.bState ? .0015f : .015f;
        var rotMultiplexer = _evrInput.FineTuneActionData.bState ? .1f : 1f;

        // Update the Calibration:Joystick : vector2 x2
        ControllerInputActions.MovePositionValues =
            new Vector3(_evrInput.LeftJoystickActionData.x * posMultiplexer,
                _evrInput.RightJoystickActionData.y * posMultiplexer,
                -_evrInput.LeftJoystickActionData.y * posMultiplexer);

        ControllerInputActions.AdjustRotationValues =
            new Vector2(_evrInput.RightJoystickActionData.y * MathF.PI / 280f * rotMultiplexer,
                -_evrInput.LeftJoystickActionData.x * MathF.PI / 280f * rotMultiplexer);
    }

    private void ParseVrEvents()
    {
        // Poll and parse all needed VR (overlay) events
        if (!Initialized || OpenVR.System is null || OpenVR.Overlay is null) return;

        var vrEvent = new VREvent_t();
        while (OpenVR.Overlay.PollNextOverlayEvent(_vrOverlayHandle,
                   ref vrEvent, (uint)Marshal.SizeOf<VREvent_t>()))
        {
            if (vrEvent.eventType != (uint)EVREventType.VREvent_Quit) continue;
            Host.Log("VREvent_Quit has been called, requesting more time for handling the exit...");

            Initialized = false; // Mark as not initialized to block all actions with requirements of such
            _ = Task.Run(() => Host.RequestExit("OpenVR shutting down!")); // 1s before shutdown
            OpenVR.System.AcknowledgeQuit_Exiting(); // We have 1s to call this, amethyst shuts down next
        }
    }

    private void UpdateBindingTexts()
    {
        if (!Initialized || OpenVR.System is null) return; // Sanity check

        // Freeze
        {
            var header = Host.RequestLocalizedString("/GeneralPage/Tips/TrackingFreeze/Header");

            // Change the tip depending on the currently connected controllers
            var controllerModel = new StringBuilder(1024);
            var error = ETrackedPropertyError.TrackedProp_Success;

            OpenVR.System.GetStringTrackedDeviceProperty(
                OpenVR.System.GetTrackedDeviceIndexForControllerRole(
                    ETrackedControllerRole.LeftHand),
                ETrackedDeviceProperty.Prop_ModelNumber_String,
                controllerModel, 1024, ref error);

            if (controllerModel.ToString().Contains("knuckles", StringComparison.OrdinalIgnoreCase) ||
                controllerModel.ToString().Contains("index", StringComparison.OrdinalIgnoreCase))
                header = header.Replace("{0}",
                    Host.RequestLocalizedString("/GeneralPage/Tips/TrackingFreeze/Buttons/Index"));

            else if (controllerModel.ToString().Contains("vive", StringComparison.OrdinalIgnoreCase))
                header = header.Replace("{0}",
                    Host.RequestLocalizedString("/GeneralPage/Tips/TrackingFreeze/Buttons/VIVE"));

            else if (controllerModel.ToString().Contains("mr", StringComparison.OrdinalIgnoreCase))
                header = header.Replace("{0}",
                    Host.RequestLocalizedString("/GeneralPage/Tips/TrackingFreeze/Buttons/WMR"));

            else
                header = header.Replace("{0}",
                    Host.RequestLocalizedString("/GeneralPage/Tips/TrackingFreeze/Buttons/Oculus"));

            ControllerInputActions.TrackingFreezeActionTitleString = header;
            ControllerInputActions.TrackingFreezeActionContentString =
                Host.RequestLocalizedString("/GeneralPage/Tips/TrackingFreeze/Footer");
        }

        // Flip
        {
            var header = Host.RequestLocalizedString("/SettingsPage/Tips/FlipToggle/Header");

            // Change the tip depending on the currently connected controllers
            var controllerModel = new StringBuilder(1024);
            var error = ETrackedPropertyError.TrackedProp_Success;

            OpenVR.System.GetStringTrackedDeviceProperty(
                OpenVR.System.GetTrackedDeviceIndexForControllerRole(
                    ETrackedControllerRole.LeftHand),
                ETrackedDeviceProperty.Prop_ModelNumber_String,
                controllerModel, 1024, ref error);

            if (controllerModel.ToString().Contains("knuckles", StringComparison.OrdinalIgnoreCase) ||
                controllerModel.ToString().Contains("index", StringComparison.OrdinalIgnoreCase))
                header = header.Replace("{0}",
                    Host.RequestLocalizedString("/SettingsPage/Tips/FlipToggle/Buttons/Index"));

            else if (controllerModel.ToString().Contains("vive", StringComparison.OrdinalIgnoreCase))
                header = header.Replace("{0}",
                    Host.RequestLocalizedString("/SettingsPage/Tips/FlipToggle/Buttons/VIVE"));

            else if (controllerModel.ToString().Contains("mr", StringComparison.OrdinalIgnoreCase))
                header = header.Replace("{0}",
                    Host.RequestLocalizedString("/SettingsPage/Tips/FlipToggle/Buttons/WMR"));

            else
                header = header.Replace("{0}",
                    Host.RequestLocalizedString("/SettingsPage/Tips/FlipToggle/Buttons/Oculus"));

            ControllerInputActions.SkeletonFlipActionTitleString = header;
            ControllerInputActions.SkeletonFlipActionContentString =
                Host.RequestLocalizedString("/SettingsPage/Tips/FlipToggle/Footer");
        }
    }

    #endregion
}

public static class OvrExtensions
{
    public static Vector3 GetPosition(this HmdMatrix34_t mat)
    {
        return new Vector3(mat.m3, mat.m7, mat.m11);
    }

    private static bool IsOrientationValid(this HmdMatrix34_t mat)
    {
        return (mat.m2 != 0 || mat.m6 != 0 || mat.m10 != 0) &&
               (mat.m1 != 0 || mat.m5 != 0 || mat.m9 != 0);
    }

    public static Quaternion GetOrientation(this HmdMatrix34_t mat)
    {
        if (!mat.IsOrientationValid()) return Quaternion.Identity;

        var q = new Quaternion
        {
            W = MathF.Sqrt(MathF.Max(0, 1 + mat.m0 + mat.m5 + mat.m10)) / 2,
            X = MathF.Sqrt(MathF.Max(0, 1 + mat.m0 - mat.m5 - mat.m10)) / 2,
            Y = MathF.Sqrt(MathF.Max(0, 1 - mat.m0 + mat.m5 - mat.m10)) / 2,
            Z = MathF.Sqrt(MathF.Max(0, 1 - mat.m0 - mat.m5 + mat.m10)) / 2
        };

        q.X = MathF.CopySign(q.X, mat.m9 - mat.m6);
        q.Y = MathF.CopySign(q.Y, mat.m2 - mat.m8);
        q.Z = MathF.CopySign(q.Z, mat.m4 - mat.m1);
        return q; // Extracted, fixed ovr quaternion!
    }
}