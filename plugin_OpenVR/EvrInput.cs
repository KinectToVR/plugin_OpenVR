using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Amethyst.Plugins.Contract;
using Valve.VR;

namespace plugin_OpenVR;

public static class EvrInput
{
    // Action strings set in action_manifest.json

    // Required
    private const string KActionSetDefault = "/actions/default"; // Default

    private const string KActionLeftJoystick = "/actions/default/in/LeftJoystick"; // Left-hand Move/Rotate Controls
    private const string KActionRightJoystick = "/actions/default/in/RightJoystick"; // Right-hand Move/Rotate Controls

    private const string KActionConfirmAndSave = "/actions/default/in/ConfirmAndSave"; // Confirm and Save
    private const string KActionModeSwap = "/actions/default/in/ModeSwap"; // Swap Move/Rotate Modes
    private const string KActionFineTune = "/actions/default/in/FineTune"; // Fine-tuning

    // Optional
    private const string KActionTrackerFreeze = "/actions/default/in/TrackerFreeze"; // Freeze Trackers
    private const string KActionFlipToggle = "/actions/default/in/FlipToggle"; // Toggle Flip

    // Main SteamEVRInput class
    public class SteamEvrInput
    {
        private IAmethystHost Host { get; set; }

        [SetsRequiredMembers]
        public SteamEvrInput(IAmethystHost host)
        {
            Host = host;
        }

        private static (uint Left, uint Right) VrControllerIndexes => (
            SteamVR.Initialized
                ? OpenVR.System?.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand) ??
                  OpenVR.k_unTrackedDeviceIndexInvalid
                : OpenVR.k_unTrackedDeviceIndexInvalid,
            SteamVR.Initialized
                ? OpenVR.System?.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand) ??
                  OpenVR.k_unTrackedDeviceIndexInvalid
                : OpenVR.k_unTrackedDeviceIndexInvalid
        );

        // Action manifest path
        private const string MActionManifestPath = "action_manifest.json";

        // Buttons data
        private InputDigitalActionData_t
            _mConfirmAndSaveData,
            _mModeSwapData,
            _mFineTuneData,
            _mTrackerFreezeData,
            _mFlipToggleData;

        private ulong _mConfirmAndSaveHandler;

        // The action sets
        private VRActiveActionSet_t
            _mDefaultActionSet;

        // Tracking Default set
        private ulong _mDefaultSetHandler;
        private ulong _mFineTuneHandler;
        private ulong _mFlipToggleHandler;

        // Calibration actions
        private ulong _mLeftJoystickHandler;

        // Analogs data
        private InputAnalogActionData_t
            _mLeftJoystickHandlerData,
            _mRightJoystickHandlerData;

        private ulong _mModeSwapHandler;
        private ulong _mRightJoystickHandler;

        // Tracking freeze actions
        private ulong _mTrackerFreezeHandler;

        // Analog data poll
        public InputAnalogActionData_t LeftJoystickActionData => _mLeftJoystickHandlerData;

        public InputAnalogActionData_t RightJoystickActionData => _mRightJoystickHandlerData;

        // Digital data poll
        public InputDigitalActionData_t ConfirmAndSaveActionData => _mConfirmAndSaveData;

        public InputDigitalActionData_t ModeSwapActionData => _mModeSwapData;

        public InputDigitalActionData_t FineTuneActionData => _mFineTuneData;

        public InputDigitalActionData_t TrackerFreezeActionData => _mTrackerFreezeData;

        public InputDigitalActionData_t TrackerFlipToggleData => _mFlipToggleData;

        // Note: SteamVR must be initialized beforehand.
        // Preferred type is (vr::VRApplication_Scene)
        public bool InitInputActions()
        {
            if (!SteamVR.Initialized || OpenVR.Input is null) return false; // Sanity check

            // Find the absolute path of manifest
            var absoluteManifestPath =
                Path.Join(GetProgramLocation().DirectoryName, MActionManifestPath);

            if (!File.Exists(absoluteManifestPath))
            {
                Host.Log("Action manifest was not found in the program " +
                         $"({GetProgramLocation().Directory}) directory.", LogSeverity.Error);
                return false; // Return failure status
            }

            // Set the action manifest. This should be in the executable directory.
            // Defined by m_actionManifestPath.
            var error = OpenVR.Input.SetActionManifestPath(absoluteManifestPath);
            if (error != EVRInputError.None)
            {
                Host.Log($"Action manifest error: {error}", LogSeverity.Error);
                return false;
            }

            /**********************************************/
            // Here, setup every action with its handler
            /**********************************************/

            // Get action handle for Left Joystick
            error = OpenVR.Input.GetActionHandle(KActionLeftJoystick, ref _mLeftJoystickHandler);
            if (error != EVRInputError.None)
            {
                Host.Log("Action handle error: {error}", LogSeverity.Error);
                return false;
            }

            // Get action handle for Right Joystick
            error = OpenVR.Input.GetActionHandle(KActionRightJoystick, ref _mRightJoystickHandler);
            if (error != EVRInputError.None)
            {
                Host.Log("Action handle error: {error}", LogSeverity.Error);
                return false;
            }

            // Get action handle for Confirm And Save
            error = OpenVR.Input.GetActionHandle(KActionConfirmAndSave, ref _mConfirmAndSaveHandler);
            if (error != EVRInputError.None)
            {
                Host.Log("Action handle error: {error}", LogSeverity.Error);
                return false;
            }

            // Get action handle for Mode Swap
            error = OpenVR.Input.GetActionHandle(KActionModeSwap, ref _mModeSwapHandler);
            if (error != EVRInputError.None)
            {
                Host.Log("Action handle error: {error}", LogSeverity.Error);
                return false;
            }

            // Get action handle for Fine-tuning
            error = OpenVR.Input.GetActionHandle(KActionFineTune, ref _mFineTuneHandler);
            if (error != EVRInputError.None)
            {
                Host.Log("Action handle error: {error}", LogSeverity.Error);
                return false;
            }

            // Get action handle for Tracker Freeze
            error = OpenVR.Input.GetActionHandle(KActionTrackerFreeze, ref _mTrackerFreezeHandler);
            if (error != EVRInputError.None)
            {
                Host.Log("Action handle error: {error}", LogSeverity.Error);
                return false;
            }

            // Get action handle for Flip Toggle
            error = OpenVR.Input.GetActionHandle(KActionFlipToggle, ref _mFlipToggleHandler);
            if (error != EVRInputError.None)
            {
                Host.Log("Action handle error: {error}", LogSeverity.Error);
                return false;
            }

            /**********************************************/
            // Here, setup every action set handle
            /**********************************************/

            // Get set handle Default Set
            error = OpenVR.Input.GetActionSetHandle(KActionSetDefault, ref _mDefaultSetHandler);
            if (error != EVRInputError.None)
            {
                Host.Log("ActionSet handle error: {error}", LogSeverity.Error);
                return false;
            }

            /**********************************************/
            // Here, setup action-set handler
            /**********************************************/

            // Default Set
            _mDefaultActionSet.ulActionSet = _mDefaultSetHandler;
            _mDefaultActionSet.ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle;
            _mDefaultActionSet.nPriority = 0;

            // Return OK
            Host.Log("EVR Input Actions initialized OK", LogSeverity.Info);
            return true;
        }

        // Update Left Joystick Action
        private bool GetLeftJoystickState()
        {
            if (!SteamVR.Initialized || OpenVR.Input is null) return false; // Sanity check

            // Update the action and grab data
            var error = OpenVR.Input.GetAnalogActionData(
                _mLeftJoystickHandler,
                ref _mLeftJoystickHandlerData,
                (uint)Marshal.SizeOf<InputAnalogActionData_t>(),
                OpenVR.k_ulInvalidInputValueHandle);

            // Return OK
            if (error == EVRInputError.None) return true;

            Host.Log($"GetAnalogActionData call error: {error}", LogSeverity.Error);
            return false;
        }

        // Update Right Joystick Action
        private bool GetRightJoystickState()
        {
            if (!SteamVR.Initialized || OpenVR.Input is null) return false; // Sanity check

            // Update the action and grab data
            var error = OpenVR.Input.GetAnalogActionData(
                _mRightJoystickHandler,
                ref _mRightJoystickHandlerData,
                (uint)Marshal.SizeOf<InputAnalogActionData_t>(),
                OpenVR.k_ulInvalidInputValueHandle);

            // Return OK
            if (error == EVRInputError.None) return true;

            Host.Log($"GetAnalogActionData call error: {error}", LogSeverity.Error);
            return false;
        }

        // Update Confirm And Save Action
        private bool GetConfirmAndSaveState()
        {
            if (!SteamVR.Initialized || OpenVR.Input is null) return false; // Sanity check

            // Update the action and grab data
            var error = OpenVR.Input.GetDigitalActionData(
                _mConfirmAndSaveHandler,
                ref _mConfirmAndSaveData,
                (uint)Marshal.SizeOf<InputDigitalActionData_t>(),
                OpenVR.k_ulInvalidInputValueHandle);

            // Return OK
            if (error == EVRInputError.None) return true;

            Host.Log($"GetDigitalActionData call error: {error}", LogSeverity.Error);
            return false;
        }

        // Update Mode Swap Action
        private bool GetModeSwapState()
        {
            if (!SteamVR.Initialized || OpenVR.Input is null) return false; // Sanity check

            // Update the action and grab data
            var error = OpenVR.Input.GetDigitalActionData(
                _mModeSwapHandler,
                ref _mModeSwapData,
                (uint)Marshal.SizeOf<InputDigitalActionData_t>(),
                OpenVR.k_ulInvalidInputValueHandle);

            // Return OK
            if (error == EVRInputError.None) return true;

            Host.Log($"GetDigitalActionData call error: {error}", LogSeverity.Error);
            return false;
        }

        // Update Fine Tune Action
        private bool GetFineTuneState()
        {
            if (!SteamVR.Initialized || OpenVR.Input is null) return false; // Sanity check

            // Update the action and grab data
            var error = OpenVR.Input.GetDigitalActionData(
                _mFineTuneHandler,
                ref _mFineTuneData,
                (uint)Marshal.SizeOf(typeof(InputDigitalActionData_t)),
                OpenVR.k_ulInvalidInputValueHandle);

            // Return OK
            if (error == EVRInputError.None) return true;

            Host.Log($"GetDigitalActionData call error: {error}", LogSeverity.Error);
            return false;
        }

        // Update Tracker Freeze Action
        private bool GetTrackerFreezeState()
        {
            if (!SteamVR.Initialized || OpenVR.Input is null) return false; // Sanity check

            // Update the action and grab data
            var error = OpenVR.Input.GetDigitalActionData(
                _mTrackerFreezeHandler,
                ref _mTrackerFreezeData,
                (uint)Marshal.SizeOf<InputDigitalActionData_t>(),
                OpenVR.k_ulInvalidInputValueHandle);

            // Return OK
            if (error == EVRInputError.None) return true;

            Host.Log($"GetDigitalActionData call error: {error}", LogSeverity.Error);
            return false;
        }

        // Update Tracker Freeze Action
        private bool GetFlipToggleState()
        {
            if (!SteamVR.Initialized || OpenVR.Input is null) return false; // Sanity check

            // Update the action and grab data
            var error = OpenVR.Input.GetDigitalActionData(
                _mFlipToggleHandler,
                ref _mFlipToggleData,
                (uint)Marshal.SizeOf<InputDigitalActionData_t>(),
                OpenVR.k_ulInvalidInputValueHandle);

            // Return OK
            if (error == EVRInputError.None) return true;

            Host.Log($"GetDigitalActionData call error: {error}", LogSeverity.Error);
            return false;
        }

        public bool UpdateActionStates()
        {
            if (!SteamVR.Initialized || OpenVR.Input is null) return false; // Sanity check

            /**********************************************/
            // Check if VR controllers are valid
            /**********************************************/

            if (VrControllerIndexes.Left == OpenVR.k_unTrackedDeviceIndexInvalid ||
                VrControllerIndexes.Right == OpenVR.k_unTrackedDeviceIndexInvalid)
                return true; // Say it's all good, refuse to elaborate, leave

            /**********************************************/
            // Here, update main action sets' handles
            /**********************************************/

            // Update Default ActionSet states
            var error = OpenVR.Input.UpdateActionState(
                new[] { _mDefaultActionSet },
                (uint)Marshal.SizeOf<VRActiveActionSet_t>());

            if (error != EVRInputError.None)
            {
                Host.Log($"ActionSet (Default) state update error: {error}", LogSeverity.Error);
                return false;
            }

            /**********************************************/
            // Here, update the actions and grab data-s
            /**********************************************/

            // Update the left joystick
            if (!GetLeftJoystickState())
            {
                Host.Log("Left Joystick Action is not active, can't update!", LogSeverity.Error);
                return false;
            }

            // Update the right joystick
            if (!GetRightJoystickState())
            {
                Host.Log("Right Joystick Action is not active, can't update!", LogSeverity.Error);
                return false;
            }

            // Update the confirm and save
            if (!GetConfirmAndSaveState())
            {
                Host.Log("Confirm And Save Action is not active, can't update!", LogSeverity.Error);
                return false;
            }

            // Update the mode swap
            if (!GetModeSwapState())
            {
                Host.Log("Mode Swap Action is not active, can't update!", LogSeverity.Error);
                return false;
            }

            // Update the fine tune
            if (!GetFineTuneState())
            {
                Host.Log("Fine-tuning Action is not active, can't update!", LogSeverity.Error);
                return false;
            }

            // Update the freeze
            // This time without checks, since this one is optional
            GetTrackerFreezeState();

            // Return OK
            return true;
        }

        public static FileInfo GetProgramLocation()
        {
            return new FileInfo(Assembly.GetExecutingAssembly().Location);
        }
    }
}