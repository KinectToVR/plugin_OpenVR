// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Amethyst.Driver.API;
using Amethyst.Plugins.Contract;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using plugin_OpenVR.Utils;
using Valve.VR;
using GridLength = ABI.Microsoft.UI.Xaml.GridLength;
using TrackerType = Amethyst.Plugins.Contract.TrackerType;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Windows.Data.Json;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace plugin_OpenVR;

public static class ServiceData
{
    public const string Name = "SteamVR";
    public const string Guid = "K2VRTEAM-AME2-APII-SNDP-SENDPTOPENVR";
}

[Export(typeof(IServiceEndpoint))]
[ExportMetadata("Name", ServiceData.Name)]
[ExportMetadata("Guid", ServiceData.Guid)]
[ExportMetadata("Publisher", "K2VR Team")]
[ExportMetadata("Website", "https://github.com/KinectToVR/plugin_OpenVR")]
public class SteamVR : IServiceEndpoint
{
    public static bool Initialized { get; private set; }
    public static object InitLock { get; } = new();

    private bool PluginLoaded { get; set; }
    private Page InterfaceRoot { get; set; }
    private Button ReManifestButton { get; set; }
    private Button ReRegisterButton { get; set; }
    private Flyout ActionFailedFlyout { get; set; }

    private GrpcChannel _channel;
    private SocketsHttpHandler _connectionHandler;
    private IK2DriverService.IK2DriverServiceClient _service;

    private EvrInput.SteamEvrInput _evrInput;
    private uint _vrNotificationId;

    private ulong _vrOverlayHandle = OpenVR.k_ulOverlayHandleInvalid;

    private Vector3 VrPlayspaceTranslation =>
        OpenVR.System.GetRawZeroPoseToStandingAbsoluteTrackingPose().GetPosition();

    private Quaternion VrPlayspaceOrientationQuaternion =>
        OpenVR.System.GetRawZeroPoseToStandingAbsoluteTrackingPose().GetOrientation();

    private int ServerDriverRpcStatus { get; set; } = -1;
    private bool ServerDriverPresent { get; set; }

    [Import(typeof(IAmethystHost))] private IAmethystHost Host { get; set; }

    public bool IsSettingsDaemonSupported => true;

    public object SettingsInterfaceRoot => InterfaceRoot;

    public int ServiceStatus { get; private set; }

    [DefaultValue("Not Defined\nE_NOT_DEFINED\nStatus message not defined!")]
    public string ServiceStatusString { get; private set; }

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
                Host.Log(
                    $"Amethyst manifest not installed! Error: {OpenVR.Applications.GetApplicationsErrorNameFromEnum(appError)}",
                    LogSeverity.Warning);
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
            if (!Initialized || OpenVR.System is null) return true; // Sanity check

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
            return OpenVR.Overlay.IsDashboardVisible();
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
            Content = Host.RequestLocalizedString("/SettingsPage/Buttons/ReManifest", ServiceData.Guid),
            Height = 40, HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness { Right = 6 }, FontSize = 16, FontWeight = FontWeights.SemiBold
        };

        ReRegisterButton = new Button
        {
            Content = Host.RequestLocalizedString("/SettingsPage/Buttons/ReRegister", ServiceData.Guid),
            Height = 40, HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness { Left = 6 }, FontSize = 16, FontWeight = FontWeights.SemiBold
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
                        Text = Host.RequestLocalizedString("/SettingsPage/ReManifest/Error/NotFound", ServiceData.Guid)
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
                        Text = Host.RequestLocalizedString("/SettingsPage/ReManifest/Error/Other", ServiceData.Guid)
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
        ReRegisterButton.Click += async (_, _) =>
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
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/SteamVRNotFound", ServiceData.Guid),
                    "", "");
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
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/OpenVRPathsError", ServiceData.Guid),
                    "", "");
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

            // Get crash handler's  parent path
            var doubleParentPath =
                Directory.GetParent(Assembly.GetExecutingAssembly().Location);

            // Search for driver manifests, try max 2 times
            var localAmethystDriverPath = "";
            for (var i = 0; i < 2; i++)
            {
                // Double that to get Amethyst exe path
                if (doubleParentPath.Parent != null)
                    doubleParentPath = doubleParentPath.Parent;

                // Find all vr driver manifests there
                var allLocalDriverManifests = Directory.GetFiles(
                    doubleParentPath.ToString(), "driver.vrdrivermanifest", SearchOption.AllDirectories);

                // For each found manifest, check if there is an ame driver dll inside
                foreach (var localDriverManifest in allLocalDriverManifests)
                    if (File.Exists(Path.Combine(
                            Directory.GetParent(localDriverManifest).ToString(),
                            "bin", "win64", "driver_Amethyst.dll")))
                    {
                        // We've found it! Now cache it and break free
                        localAmethystDriverPath = Directory.GetParent(localDriverManifest).ToString();
                        goto p_search_loop_end;
                    }
                // Else redo once more & then check
            }

            // End of the searching loop
            p_search_loop_end:

            // If there's none (still), cry about it and abort
            if (string.IsNullOrEmpty(localAmethystDriverPath))
            {
                await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/DriverNotFound", ServiceData.Guid),
                    "", "");
                return;
            }

            /* 2 */

            // Force exit (kill) SteamVR
            if (Process.GetProcesses().FirstOrDefault(
                    proc => proc.ProcessName is "vrserver" or "vrmonitor") != null)
            {
                if (await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                        Host?.RequestLocalizedString("/CrashHandler/ReRegister/KillSteamVR/Content", ServiceData.Guid),
                        Host?.RequestLocalizedString("/CrashHandler/ReRegister/KillSteamVR/PrimaryButton",
                            ServiceData.Guid),
                        Host?.RequestLocalizedString("/CrashHandler/ReRegister/KillSteamVR/SecondaryButton",
                            ServiceData.Guid)))
                    await Task.Factory.StartNew(() =>
                    {
                        Shutdown(); // Exit not to be killed
                        Host.RefreshStatusInterface();
                        return helper.CloseSteamVr();
                    });
                else
                    return;
            }

            /* 2.5 */

            // Search for all K2EX instances and either unregister or delete them


            var isDriverK2Present = resultPaths.Exists.CopiedDriverExists; // is ame copied?
            var driverK2PathsList = new List<string>(); // ame external list

            foreach (var externalDriver in openVrPaths.external_drivers.Where(
                         externalDriver => externalDriver.Contains("KinectToVR")))
            {
                isDriverK2Present = true;
                driverK2PathsList.Add(externalDriver);
            }

            // Remove (or delete) the existing K2EX Drivers
            if (isDriverK2Present && await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/ExistingDrivers/Content_K2EX",
                        ServiceData.Guid),
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/ExistingDrivers/PrimaryButton_K2EX",
                        ServiceData.Guid),
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/ExistingDrivers/SecondaryButton_K2EX",
                        ServiceData.Guid)))
                return;

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
                        foreach (var driverK2Path in driverK2PathsList)
                            openVrPaths.external_drivers.Remove(driverK2Path);

                        // Save it
                        openVrPaths.Write();
                    }
                }
            }
            catch (Exception)
            {
                // Critical, cry about it
                await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/FatalRemoveException_K2EX",
                        ServiceData.Guid),
                    "", "");
                return;
            }

            /* 3 */

            // Search for all remaining (registered or copied) Amethyst Driver instances

            var isAmethystDriverPresent = resultPaths.Exists.CopiedDriverExists; // is ame copied?
            var amethystDriverPathsList = new List<string>(); // ame external list

            var isLocalAmethystDriverRegistered = false; // is our local ame registered?

            foreach (var externalDriver in openVrPaths.external_drivers.Where(
                         externalDriver => externalDriver.Contains("Amethyst")))
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
            if (isAmethystDriverPresent && !await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton,
                    Host,
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/ExistingDrivers/Content",
                        ServiceData.Guid),
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/ExistingDrivers/PrimaryButton",
                        ServiceData.Guid),
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/ExistingDrivers/SecondaryButton",
                        ServiceData.Guid)))
                return;

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
                        foreach (var amethystDriverPath in amethystDriverPathsList
                                     .Where(amethystDriverPath => amethystDriverPath != localAmethystDriverPath))
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
                    Host?.RequestLocalizedString("/CrashHandler/ReRegister/FatalRemoveException",
                        ServiceData.Guid),
                    "", "");
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
                            Host?.RequestLocalizedString("/CrashHandler/ReRegister/OpenVRPathsWriteError",
                                ServiceData.Guid),
                            "", "");
                        return;
                    }
                }
                catch (Exception)
                {
                    // Critical, cry about it
                    await ConfirmationFlyout.HandleButtonConfirmationFlyout(ReRegisterButton, Host,
                        Host?.RequestLocalizedString("/CrashHandler/ReRegister/FatalRegisterException",
                            ServiceData.Guid),
                        "", "");
                    return;
                }

            /* 5 */

            // Try-Catch it
            try
            {
                // Read the vr settings
                var steamVrSettings = JsonObject.Parse(
                    await File.ReadAllTextAsync(resultPaths.Path.VrSettingsPath));

                // Enable & unblock the Amethyst Driver
                steamVrSettings.Remove("driver_Amethyst");
                steamVrSettings.Add("driver_Amethyst", new JsonObject
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
                Host?.RequestLocalizedString("/CrashHandler/ReRegister/Finished",
                    ServiceData.Guid),
                "", "");
        };

        InterfaceRoot = new Page
        {
            Content = new Grid
            {
                Children = { ReManifestButton, ReRegisterButton },
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new Microsoft.UI.Xaml.GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new Microsoft.UI.Xaml.GridLength(1, GridUnitType.Star) }
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

        // Initialize OpenVR
        if (!OpenVrStartup())
        {
            ServiceStatus = 1;
            return 1;
        }

        // Install the manifest
        InstallVrApplicationManifest();

        // Startup input actions
        var serviceStatus = 0;
        if (!EvrActionsStartup()) serviceStatus = 2;

        // Connect to the server driver
        K2ServerDriverRefresh();

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
        {
            Initialized = false; // vrClient dll unloaded
            OpenVR.Shutdown(); // Shutdown OpenVR

            ServiceStatus = 1; // Update VR status
            K2ServerDriverRefresh();
        }
    }

    public void DisplayToast((string Title, string Text) message)
    {
        if (_vrOverlayHandle == OpenVR.k_ulOverlayHandleInvalid ||
            string.IsNullOrEmpty(message.Title) ||
            string.IsNullOrEmpty(message.Text)) return;

        // Hide the current notification (if being shown)
        if (_vrNotificationId != 0) // If valid
            OpenVR.Notifications.RemoveNotification(_vrNotificationId);

        // Empty image data
        var notificationBitmap = new NotificationBitmap_t();

        // null is the icon/image texture
        OpenVR.Notifications.CreateNotification(
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
            return _service?.RequestVRRestart(new ServiceRequest
                { Message = reason, WantReply = wantReply }).State;
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
            if (!Initialized || OpenVR.System is null || _service is null || ServiceStatus != 0)
                return wantReply ? new List<(TrackerBase Tracker, bool Success)>() : null;

            var call = _service?.SetTrackerStateVector();
            if (call is null) return wantReply ? new List<(TrackerBase Tracker, bool Success)>() : null;

            foreach (var tracker in trackerBases)
                await call.RequestStream.WriteAsync(
                    new ServiceRequest
                    {
                        WantReply = wantReply,
                        TrackerStateTuple = new Service_TrackerStatePair
                        {
                            State = tracker.ConnectionState,
                            TrackerType = (Amethyst.Driver.API.TrackerType)tracker.Role
                        }
                    });

            await call.RequestStream.CompleteAsync();
            return wantReply
                ? call.ResponseStream.ReadAllAsync().ToBlockingEnumerable()
                    .Select(x => (new TrackerBase
                    {
                        Role = (TrackerType)x.TrackerType
                    }, x.State))
                : null;
        }
        catch (Exception)
        {
            return wantReply ? new List<(TrackerBase Tracker, bool Success)>() : null;
        }
    }

    public async Task<IEnumerable<(TrackerBase Tracker, bool Success)>> UpdateTrackerPoses(
        IEnumerable<TrackerBase> trackerBases, bool wantReply = true)
    {
        try
        {
            // Driver client sanity check: return empty or null if not valid
            if (!Initialized || OpenVR.System is null || _service is null || ServiceStatus != 0)
                return wantReply ? new List<(TrackerBase Tracker, bool Success)>() : null;

            using var call = _service?.UpdateTrackerVector();
            if (call is null) return wantReply ? new List<(TrackerBase Tracker, bool Success)>() : null;

            foreach (var tracker in trackerBases)
                await call.RequestStream.WriteAsync(
                    new ServiceRequest
                    {
                        WantReply = true,
                        TrackerBase = new K2TrackerBase
                        {
                            Tracker = (Amethyst.Driver.API.TrackerType)tracker.Role,
                            Data = new K2TrackerData
                            {
                                IsActive = tracker.ConnectionState,
                                Role = (Amethyst.Driver.API.TrackerType)tracker.Role,
                                Serial = tracker.Serial
                            },
                            Pose = new K2TrackerPose
                            {
                                Orientation = tracker.Orientation.K2Quaternion(),
                                Position = tracker.Position.K2Vector3(),
                                Physics = new K2TrackerPhysics
                                {
                                    Velocity = tracker.Velocity?.K2Vector3(),
                                    Acceleration = tracker.Acceleration?.K2Vector3(),
                                    AngularVelocity = tracker.AngularVelocity?.K2Vector3(),
                                    AngularAcceleration = tracker.AngularAcceleration?.K2Vector3()
                                }
                            }
                        }
                    });

            await call.RequestStream.CompleteAsync();
            return wantReply
                ? call.ResponseStream.ReadAllAsync().ToBlockingEnumerable()
                    .Select(x => (new TrackerBase
                    {
                        Role = (TrackerType)x.TrackerType
                    }, x.State))
                : null;
        }
        catch (Exception)
        {
            return wantReply ? new List<(TrackerBase Tracker, bool Success)>() : null;
        }
    }

    public async Task<(int Status, string StatusMessage, long PingTime)> TestConnection()
    {
        try
        {
            // Refresh the driver, just in case
            K2ServerDriverRefresh();

            // Driver client sanity check: return empty or null if not valid
            if (!Initialized || OpenVR.System is null || _service is null ||
                ServiceStatus != 0) return (-1, "SERVICE_INVALID", 0);

            // Grab the current time and send the message
            await Task.Delay(1); // Simulate an async job
            var messageSendTimeStopwatch = new Stopwatch();

            messageSendTimeStopwatch.Start();
            _service?.PingDriverService(new Empty());
            messageSendTimeStopwatch.Stop();

            // Return tuple with response and elapsed time
            return (0, "OK", messageSendTimeStopwatch.ElapsedTicks);
        }
        catch (RpcException e)
        {
            ServiceStatus = e.StatusCode == StatusCode.Unavailable ? -1 : -10;
            ServerDriverRpcStatus = (int)e.StatusCode;
            return (-1, $"EXCEPTION {e.Message}", 0);
        }
        catch (Exception e)
        {
            ServiceStatus = -10;
            ServerDriverRpcStatus = (int)StatusCode.Unknown;
            return (-1, $"EXCEPTION {e.Message}", 0);
        }
    }

    #region Amethyst VRDriver Methods

    public int InitAmethystServer(string target = "http://localhost:7135")
    {
        try
        {
            // Create the handler
            _connectionHandler = new SocketsHttpHandler
            {
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            };

            // Compose the channel arguments
            var channelOptions = new GrpcChannelOptions
            {
                Credentials = ChannelCredentials.Insecure,
                HttpHandler = _connectionHandler
            };

            // Create the RPC channel
            _channel = GrpcChannel.ForAddress(target, channelOptions);

            // Create the RPC messaging service
            _service = new IK2DriverService.IK2DriverServiceClient(_channel);
        }
        catch (Exception e)
        {
            Host.Log(e.ToString(), LogSeverity.Error);
            return -10;
        }

        return 0;
    }

    public (int ServerStatus, int APIStatus) CheckK2ServerStatus()
    {
        // Don't check if OpenVR failed
        if (ServiceStatus == 1) return (1, (int)StatusCode.Aborted);
        
        try
        {
            /* Initialize the port */
            Host.Log("Initializing the server IPC...", LogSeverity.Info);
            var initCode = InitAmethystServer();

            Host.Log($"Server IPC initialization {(initCode == 0 ? "succeed" : "failed")}, exit code: {initCode}",
                initCode == 0 ? LogSeverity.Info : LogSeverity.Error);

            try
            {
                // Driver client sanity check: return empty or null if not valid
                if (!Initialized || OpenVR.System is null) 
                    return (1, (int)StatusCode.Unknown);

                if (_channel is null || _service is null) 
                    return (-1, (int)StatusCode.Unknown);

                // Grab the current time and send the message
                _service?.PingDriverService(new Empty());

                // Return tuple with response and elapsed time
                return (0, (int)StatusCode.OK);
            }
            catch (RpcException e)
            {
                ServiceStatus = e.StatusCode == StatusCode.Unavailable ? -1 : -10;
                ServerDriverRpcStatus = (int)e.StatusCode;
                return (-1, (int)e.StatusCode);
            }
            catch (Exception)
            {
                ServiceStatus = -10;
                ServerDriverRpcStatus = (int)StatusCode.Unknown;
                return (-1, (int)StatusCode.OK);
            }
        }
        catch (Exception e)
        {
            Host.Log("Server status check failed! " +
                     $"Exception: {e.Message}", LogSeverity.Warning);

            return (-10, (int)StatusCode.Unknown);
        }

        /*
         * codes:
            all ok: 0
            server could not be reached: -1
            exception when trying to reach: -10
            could not create rpc channel: -2
            could not create rpc stub: -3

            fatal run-time failure: 10
         */
    }

    public void K2ServerDriverRefresh()
    {
        (ServiceStatus, ServerDriverRpcStatus) = CheckK2ServerStatus();
        ServerDriverPresent = false; // Assume fail
        ServiceStatusString = Host.RequestLocalizedString("/ServerStatuses/WTF", ServiceData.Guid);
        //"COULD NOT CHECK STATUS (\u15dc\u02ec\u15dc)\nE_WTF\nSomething's fucked a really big time.";

        switch (ServiceStatus)
        {
            case 0:
                ServiceStatusString = Host.RequestLocalizedString("/ServerStatuses/Success", ServiceData.Guid)
                    .Replace("{0}", ServiceStatus.ToString())
                    .Replace("{1}", ServerDriverRpcStatus.ToString());
                //"Success! (Code 1)\nI_OK\nEverything's good!";

                ServerDriverPresent = true;
                break; // Change to success

            case 1:
                ServiceStatusString = Host.RequestLocalizedString("/ServerStatuses/OpenVRError", ServiceData.Guid)
                    .Replace("{0}", ServiceStatus.ToString())
                    .Replace("{1}", ServerDriverRpcStatus.ToString());
                //"OPENVR INIT ERROR (Code {0}:{1})\nE_OVRINIT_ERROR\nCheck if SteamVR is running, and your VR headset is connected properly to it.";
                break;

            case 2:
                ServiceStatusString = Host.RequestLocalizedString("/ServerStatuses/IVRInputError", ServiceData.Guid)
                    .Replace("{0}", ServiceStatus.ToString())
                    .Replace("{1}", ServerDriverRpcStatus.ToString());
                //"OPENVR INIT ERROR (Code {0}:{1})\nE_OVRINIT_ERROR\nCheck if SteamVR is running, and your VR headset is connected properly to it.";
                break;

            case -1:
                ServiceStatusString = Host.RequestLocalizedString("/ServerStatuses/ConnectionError", ServiceData.Guid)
                    .Replace("{0}", ServiceStatus.ToString())
                    .Replace("{1}", ServerDriverRpcStatus.ToString());
                //"SERVER CONNECTION ERROR (Code -1:{0})\nE_CONNECTION_ERROR\nCheck SteamVR add-ons (NOT overlays) and enable Amethyst.";
                break;

            case -10:
                ServiceStatusString = Host.RequestLocalizedString("/ServerStatuses/Exception", ServiceData.Guid)
                    .Replace("{0}", ServiceStatus.ToString())
                    .Replace("{1}", ServerDriverRpcStatus.ToString());
                //"EXCEPTION WHILE CHECKING (Code -10)\nE_EXCEPTION_WHILE_CHECKING\nCheck SteamVR add-ons (NOT overlays) and enable Amethyst.";
                break;

            case -2:
                ServiceStatusString = Host.RequestLocalizedString("/ServerStatuses/RPCChannelFailure", ServiceData.Guid)
                    .Replace("{0}", ServiceStatus.ToString())
                    .Replace("{1}", ServerDriverRpcStatus.ToString());
                //"RPC CHANNEL FAILURE (Code -2:{0})\nE_RPC_CHAN_FAILURE\nCould not connect to localhost:7135, is it already taken?";
                break;

            case -3:
                ServiceStatusString = Host.RequestLocalizedString("/ServerStatuses/RPCStubFailure", ServiceData.Guid)
                    .Replace("{0}", ServiceStatus.ToString())
                    .Replace("{1}", ServerDriverRpcStatus.ToString());
                //"RPC/API STUB FAILURE (Code -3:{0})\nE_RPC_STUB_FAILURE\nCould not derive IK2DriverService! Is the protocol valid?";
                break;

            case 10:
                ServiceStatusString = Host.RequestLocalizedString("/ServerStatuses/ServerFailure", ServiceData.Guid)
                    .Replace("{0}", ServiceStatus.ToString());
                //"FATAL SERVER FAILURE (Code 10)\nE_FATAL_SERVER_FAILURE\nPlease restart, check logs and write to us on Discord.";
                break;

            default:
                ServiceStatusString = Host.RequestLocalizedString("/ServerStatuses/WTF", ServiceData.Guid);
                //"COULD NOT CHECK STATUS (\u15dc\u02ec\u15dc)\nE_WTF\nSomething's fucked a really big time.";
                break;
        }
    }

    #endregion

    #region OpenVR Interfacing Methods

    public bool OpenVrStartup()
    {
        Host.Log("Attempting connection to VRSystem... ", LogSeverity.Info);

        try
        {
            Host.Log("Creating a cancellation token...", LogSeverity.Info);
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(7));

            Host.Log("Waiting for the VR System to initialize...", LogSeverity.Info);
            var eError = EVRInitError.None;

            OpenVR.Init(ref eError, EVRApplicationType.VRApplication_Overlay);
            Initialized = true; // vrClient dll loaded

            Host.Log("The VRSystem finished initializing...", LogSeverity.Info);
            if (eError != EVRInitError.None)
            {
                Host.Log($"IVRSystem could not be initialized: EVRInitError Code {eError}", LogSeverity.Error);
                return false; // Catastrophic failure!
            }
        }
        catch (Exception e)
        {
            Host.Log($"The VR System took too long to initialize ({e.Message}), giving up!", LogSeverity.Error);
            return false; // Took too long to initialize, abort!
        }

        // We're good to go!
        Host.Log("Looks like the VR System is ready to go!", LogSeverity.Info);

        // Initialize the overlay
        OpenVR.Overlay.CreateOverlay("k2vr.amethyst.desktop", "Amethyst", ref _vrOverlayHandle);

        Host.Log($"VR Playspace translation: \n{VrPlayspaceTranslation}", LogSeverity.Info);
        Host.Log($"VR Playspace orientation: \n{VrPlayspaceOrientationQuaternion}", LogSeverity.Info);
        return true; // OK
    }

    public int InstallVrApplicationManifest()
    {
        if (!Initialized || OpenVR.Applications is null) return 0; // Sanity check
        if (OpenVR.Applications.IsApplicationInstalled("K2VR.Amethyst"))
        {
            Host.Log("Amethyst manifest is already installed", LogSeverity.Info);
            return 0;
        }

        if (File.Exists(Path.Join(Assembly.GetAssembly(GetType())!.Location, "Amethyst.vrmanifest")))
        {
            var appError = OpenVR.Applications.AddApplicationManifest(
                Path.Join(Assembly.GetAssembly(GetType())!.Location, "Amethyst.vrmanifest"), false);

            if (appError != EVRApplicationError.None)
            {
                Host.Log($"Amethyst manifest not installed! Error: {appError}", LogSeverity.Warning);
                return -1;
            }

            Host.Log("Amethyst manifest installed at: " +
                     $"{Path.Join(Assembly.GetAssembly(GetType())!.Location, "Amethyst.vrmanifest")}",
                LogSeverity.Info);
            return 0;
        }

        Host.Log(
            $"Amethyst vr manifest ({Path.Join(Assembly.GetAssembly(GetType())!.Location, "Amethyst.vrmanifest")}) not found!",
            LogSeverity.Warning);
        return -2;
    }

    public bool EvrActionsStartup()
    {
        Host.Log("Attempting to set up EVR Input Actions...", LogSeverity.Info);

        if (!_evrInput.InitInputActions())
        {
            Host.Log("Could not set up Input Actions. Please check the upper log for further information.",
                LogSeverity.Error);
            DisplayToast(("EVR Input Actions Init Failure!",
                "Couldn't set up Input Actions. Please check the log file for further information."));
            return false;
        }

        Host.Log("EVR Input Actions set up OK", LogSeverity.Info);
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
            Host.Log("[Input Actions] Input: Tracking freeze toggled", LogSeverity.Info);
            ControllerInputActions.TrackingFreezeToggled?.Invoke(this, EventArgs.Empty);
        }

        // Update the Flip Toggle : toggle
        // Only if the state has changed from 1 to 0: button was clicked
        if (!_evrInput.TrackerFlipToggleData.bState && bFlipToggleState)
        {
            Host.Log("[Input Actions] Input: Flip toggled", LogSeverity.Info);
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
            new Vector2(_evrInput.LeftJoystickActionData.x * MathF.PI / 280f * rotMultiplexer,
                _evrInput.RightJoystickActionData.y * MathF.PI / 280f * rotMultiplexer);
    }

    private void ParseVrEvents()
    {
        // Poll and parse all needed VR (overlay) events
        if (!Initialized || OpenVR.System is null) return;

        var vrEvent = new VREvent_t();
        while (OpenVR.Overlay.PollNextOverlayEvent(_vrOverlayHandle,
                   ref vrEvent, (uint)Marshal.SizeOf<VREvent_t>()))
        {
            if (vrEvent.eventType != (uint)EVREventType.VREvent_Quit) continue;

            Host.Log("VREvent_Quit has been called, requesting more time for handling the exit...", LogSeverity.Info);

            OpenVR.System.AcknowledgeQuit_Exiting();
            Host.RequestExit("OpenVR shutting down!", ServiceData.Guid);
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

    public static K2Vector3 K2Vector3(this Vector3 v)
    {
        return new K2Vector3 { X = v.X, Y = v.Y, Z = v.Z };
    }

    public static K2Quaternion K2Quaternion(this Quaternion q)
    {
        return new K2Quaternion { W = q.W, X = q.X, Y = q.Y, Z = q.Z };
    }
}