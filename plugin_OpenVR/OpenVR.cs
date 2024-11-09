// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amethyst.Plugins.Contract;
using com.driver_Amethyst;
using Microsoft.UI.Xaml.Controls;
using plugin_OpenVR.Pages;
using plugin_OpenVR.Utils;
using Valve.VR;
using Vanara.PInvoke;

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace plugin_OpenVR;

[Export(typeof(IServiceEndpoint))]
[ExportMetadata("Name", "SteamVR")]
[ExportMetadata("Guid", "K2VRTEAM-AME2-APII-SNDP-SENDPTOPENVR")]
[ExportMetadata("Publisher", "K2VR Team")]
[ExportMetadata("Version", "1.0.0.2")]
[ExportMetadata("Website", "https://github.com/KinectToVR/plugin_OpenVR")]
[ExportMetadata("DependencyInstaller", typeof(DriverInstaller))]
[ExportMetadata("CoreSetupData", typeof(SetupData))]
public class SteamVR : IServiceEndpoint
{
    private IDriverService _driverService;
    private uint _vrNotificationId;
    private ulong _vrOverlayHandle = OpenVR.k_ulOverlayHandleInvalid;

    public SteamEvrInput VrInput { get; set; }
    public static bool Initialized { get; private set; }
    private static object InitLock { get; } = new();

    private bool PluginLoaded { get; set; }
    private Page InterfaceRoot { get; set; }
    private SettingsPage Settings { get; set; }

    private Vector3 VrPlayspaceTranslation =>
        OpenVR.System.GetRawZeroPoseToStandingAbsoluteTrackingPose().GetPosition();

    private Quaternion VrPlayspaceOrientationQuaternion =>
        OpenVR.System.GetRawZeroPoseToStandingAbsoluteTrackingPose().GetOrientation();

    private Exception ServerDriverException { get; set; }
    private bool ServerDriverPresent => ServiceStatus == 0;

    [Import(typeof(IAmethystHost))] private IAmethystHost Host { get; set; }

    public static IAmethystHost HostStatic { get; set; }

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

    public Dictionary<TrackerType, SortedSet<IKeyInputAction>> SupportedInputActions => [];

    public SortedSet<TrackerType> AdditionalSupportedTrackerTypes =>
    [
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
    ];

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
            Settings?.InstallVrApplicationManifest(); // Just in case
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
        VrInput ??= new SteamEvrInput(Host);
        Settings ??= new SettingsPage { DataParent = this, Host = Host };
        InterfaceRoot = new Page
        {
            Content = Settings
        };

        HostStatic = Host;
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
        Settings?.InstallVrApplicationManifest();

        // Update bindings
        UpdateBindingTexts();

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
        lock (Host.UpdateThreadLock)
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
            _driverService.RequestVrRestart(reason);
            return true; // Wait and return
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
            if (!Initialized || OpenVR.System is null || _driverService is null || ServiceStatus != 0)
                return wantReply ? new List<(TrackerBase Tracker, bool Success)>() : null;

            var enumTrackerBases = trackerBases.ToList();
            foreach (var trackerBase in enumTrackerBases.ToList())
                _driverService.SetTrackerState(trackerBase.ComTracker());

            return wantReply ? enumTrackerBases.Select(x => (x, true)) : null;
        }
        catch (Exception e)
        {
            Host?.Log($"Failed to update one or more trackers, exception: {e.Message}");
            return wantReply ? new List<(TrackerBase Tracker, bool Success)> { (null, false) } : null;
        }
    }

    public async Task<IEnumerable<(TrackerBase Tracker, bool Success)>> UpdateTrackerPoses(
        IEnumerable<TrackerBase> trackerBases, bool wantReply = true, CancellationToken? token = null)
    {
        try
        {
            // Driver client sanity check: return empty or null if not valid
            if (!Initialized || OpenVR.System is null || _driverService is null || ServiceStatus != 0)
                return wantReply ? new List<(TrackerBase Tracker, bool Success)>() : null;

            var enumTrackerBases = trackerBases.ToList();
            foreach (var trackerBase in enumTrackerBases.ToList())
                _driverService.UpdateTracker(trackerBase.ComTracker());

            return wantReply ? enumTrackerBases.Select(x => (x, true)) : null;
        }
        catch (Exception e)
        {
            Host?.Log($"Failed to update one or more trackers, exception: {e.Message}");
            return wantReply ? new List<(TrackerBase Tracker, bool Success)>() : null;
        }
    }

    public Task ProcessKeyInput(IKeyInputAction action, object data, TrackerType? receiver, CancellationToken? token = null)
    {
        Host?.Log("Input actions are not supported with emulation disabled.");
        return Task.CompletedTask;
    }

    public async Task<(int Status, string StatusMessage, long PingTime)> TestConnection()
    {
        try
        {
            // Update bindings
            UpdateBindingTexts();

            // Refresh the driver, just in case
            K2ServerDriverRefresh();

            // Driver client sanity check: return empty or null if not valid
            if (!Initialized || OpenVR.System is null || _driverService is null ||
                ServiceStatus != 0) return (-1, "SERVICE_INVALID", 0);

            // Grab the current time and send the message
            var messageSendTimeStopwatch = new Stopwatch();

            messageSendTimeStopwatch.Start();
            _driverService.PingDriverService(out var ms);
            messageSendTimeStopwatch.Stop();

            // Return tuple with response and elapsed time
            Host.Log($"Ping: {ms - DateTimeOffset.Now.ToUnixTimeMilliseconds()}ms");
            return (0, "OK", messageSendTimeStopwatch.ElapsedTicks);
        }
        catch (Exception e)
        {
            ServiceStatus = -10;
            ServerDriverException = e;
            return (-1, $"EXCEPTION {e.Message}", 0);
        }
    }

    #region Amethyst VRDriver Methods

    private int InitAmethystServerAsync(string target = "BA32B754-20E3-4C8C-913B-28BBAC30531B")
    {
        try
        {
            Host?.Log("Registering the COM proxy/stub...");
            ((HRESULT)DriverHelper.InstallProxyStub()).ThrowIfFailed();

            Host?.Log("Searching for the COM driver service...");
            var guid = Guid.Parse(target);

            DriverHelper.GetActiveObject(ref guid, IntPtr.Zero, out var service);

            Host?.Log($"Trying to cast the service into {typeof(IDriverService)}...");
            _driverService = (IDriverService)service;

            Host?.Log($"{nameof(service)} is {typeof(IDriverService)}!");
        }
        catch (COMException e)
        {
            Host?.Log(e.ToString(), LogSeverity.Error);
            ServerDriverException = e;
            return -1;
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

    private int CheckK2ServerStatusAsync()
    {
        // Don't check if OpenVR failed
        if (ServiceStatus == 1) return 1;

        try
        {
            /* Initialize the port */
            Host.Log("Initializing the server IPC...");
            var initCode = InitAmethystServerAsync();

            Host.Log($"Server IPC initialization {(initCode == 0 ? "succeed" : "failed")}, exit code: {initCode}",
                initCode == 0 ? LogSeverity.Info : LogSeverity.Error);

            try
            {
                // Driver client sanity check: return empty or null if not valid
                if (!Initialized || OpenVR.System is null)
                    return 1;

                if (initCode != 0)
                    return initCode;

                if (_driverService is null)
                    return -2;

                // Grab the current time and send the message
                _driverService.PingDriverService(out var ms);
                Host.Log($"Ping: {ms - DateTimeOffset.Now.ToUnixTimeMilliseconds()}ms");

                return 0; // Everything should be fine
            }
            catch (COMException e)
            {
                Host.Log(e.ToString(), LogSeverity.Error);
                ServerDriverException = e;
                return -1;
            }
            catch (Exception e)
            {
                Host.Log(e.ToString(), LogSeverity.Error);
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

    private void K2ServerDriverRefresh()
    {
        ServiceStatus = CheckK2ServerStatusAsync();

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

    private bool EvrActionsStartup()
    {
        Host.Log("Attempting to set up EVR Input Actions...");

        if (!VrInput.InitInputActions())
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
        var bFreezeState = VrInput.TrackerFreezeActionData;
        var bFlipToggleState = VrInput.TrackerFlipToggleData;

        // Update all input actions
        if (!VrInput.UpdateActionStates())
            Host.Log("Could not update EVR Input Actions. Please check logs for further information",
                LogSeverity.Error);

        // Update the Tracking Freeze : toggle
        // Only if the state has changed from 1 to 0: button was clicked
        if (!VrInput.TrackerFreezeActionData && bFreezeState)
        {
            Host.Log("[Input Actions] Input: Tracking freeze toggled");
            ControllerInputActions.TrackingFreezeToggled?.Invoke(this, EventArgs.Empty);
        }

        // Update the Flip Toggle : toggle
        // Only if the state has changed from 1 to 0: button was clicked
        if (!VrInput.TrackerFlipToggleData && bFlipToggleState)
        {
            Host.Log("[Input Actions] Input: Flip toggled");
            ControllerInputActions.SkeletonFlipToggled?.Invoke(this, EventArgs.Empty);
        }

        // Update the Calibration:Confirm : one-time switch
        // Only one-way switch this time, reset at calibration's end
        if (VrInput.ConfirmAndSaveActionData)
            ControllerInputActions.CalibrationConfirmed?.Invoke(this, EventArgs.Empty);

        // Update the Calibration:ModeSwap : one-time switch
        // Only if the state has changed from 1 to 0: chord was done
        if (VrInput.ModeSwapActionData)
            ControllerInputActions.CalibrationModeChanged?.Invoke(this, EventArgs.Empty);

        // Update the Calibration:FineTune : held switch
        var posMultiplexer = VrInput.FineTuneActionData ? .0015f : .015f;
        var rotMultiplexer = VrInput.FineTuneActionData ? .1f : 1f;

        // Update the Calibration:Joystick : vector2 x2
        ControllerInputActions.MovePositionValues =
            new Vector3(VrInput.LeftJoystickActionData.X * posMultiplexer,
                VrInput.RightJoystickActionData.Y * posMultiplexer,
                -VrInput.LeftJoystickActionData.Y * posMultiplexer);

        ControllerInputActions.AdjustRotationValues =
            new Vector2(VrInput.RightJoystickActionData.Y * MathF.PI / 280f * rotMultiplexer,
                -VrInput.LeftJoystickActionData.X * MathF.PI / 280f * rotMultiplexer);
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
    public static dTrackerBase ComTracker(this TrackerBase tracker)
    {
        return new dTrackerBase
        {
            ConnectionState = Convert.ToSByte(tracker.ConnectionState),
            TrackingState = Convert.ToSByte(tracker.TrackingState == TrackedJointState.StateTracked),
            Serial = tracker.Serial,
            Role = (dTrackerType)tracker.Role,
            Position = tracker.Position.ComVector(),
            Orientation = tracker.Orientation.ComQuaternion(),
            Velocity = tracker.Velocity.ComVector(),
            Acceleration = tracker.Acceleration.ComVector(),
            AngularVelocity = tracker.AngularVelocity.ComVector(),
            AngularAcceleration = tracker.AngularAcceleration.ComVector()
        };
    }

    public static dVector3 ComVector(this Vector3 v)
    {
        return new dVector3 { X = v.X, Y = v.Y, Z = v.Z };
    }

    public static dVector3Nullable ComVector(this Vector3? v)
    {
        return new dVector3Nullable
            { HasValue = Convert.ToSByte(v.HasValue), Value = v?.ComVector() ?? new dVector3() };
    }

    public static dQuaternion ComQuaternion(this Quaternion q)
    {
        return new dQuaternion { X = q.X, Y = q.Y, Z = q.Z, W = q.W };
    }

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