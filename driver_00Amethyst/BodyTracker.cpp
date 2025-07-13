#include <openvr_driver.h>
#include "BodyTracker.h"
#include <ranges>

BodyTracker::BodyTracker(const std::string& serial, const ITrackerType role) : _type(role)
{
    _serial = serial;
    _role = static_cast<int>(role);
    _active = false;

    _pose = {0};
    _pose.poseIsValid = true; // Otherwise tracker may disappear
    _pose.result = vr::TrackingResult_Running_OK;
    _pose.deviceIsConnected = false;

    // OpenVR Space Calibration : done on client side
    _pose.qWorldFromDriverRotation.w = 1;
    _pose.qWorldFromDriverRotation.x = 0;
    _pose.qWorldFromDriverRotation.y = 0;
    _pose.qWorldFromDriverRotation.z = 0;

    // OpenVR Driver Calibration : done on client side
    _pose.qDriverFromHeadRotation.w = 1;
    _pose.qDriverFromHeadRotation.x = 0;
    _pose.qDriverFromHeadRotation.y = 0;
    _pose.qDriverFromHeadRotation.z = 0;

    // Position
    _pose.vecPosition[0] = 0;
    _pose.vecPosition[1] = 0;
    _pose.vecPosition[2] = 0;

    // Rotation
    _pose.qRotation.w = 1;
    _pose.qRotation.x = 0;
    _pose.qRotation.y = 0;
    _pose.qRotation.z = 0;

    // Velocity
    _pose.vecVelocity[0] = 0;
    _pose.vecVelocity[1] = 0;
    _pose.vecVelocity[2] = 0;

    // Acceleration
    _pose.vecAcceleration[0] = 0;
    _pose.vecAcceleration[1] = 0;
    _pose.vecAcceleration[2] = 0;

    // Angular Velocity
    _pose.vecAngularVelocity[0] = 0;
    _pose.vecAngularVelocity[1] = 0;
    _pose.vecAngularVelocity[2] = 0;

    // Angular Acceleration
    _pose.vecAngularAcceleration[0] = 0;
    _pose.vecAngularAcceleration[1] = 0;
    _pose.vecAngularAcceleration[2] = 0;

    // Input components
    if (role == Tracker_LeftHand)
    {
        boolean_components_ = {
            {"/input/system/click", 0},
            {"/input/x/click", 0},
            {"/input/x/touch", 0},
            {"/input/y/click", 0},
            {"/input/y/touch", 0},
            {"/input/trigger/touch", 0},
            {"/input/grip/touch", 0},
            {"/input/joystick/click", 0},
            {"/input/joystick/touch", 0},
        };

        scalar_components_ = {
            {"/input/grip/value", 0},
            {"/input/trigger/value", 0},
            {"/input/joystick/x", 1},
            {"/input/joystick/y", 1}
        };
    }
    else if (role == Tracker_RightHand)
    {
        boolean_components_ = {
            {"/input/system/click", 0},
            {"/input/a/click", 0},
            {"/input/a/touch", 0},
            {"/input/b/click", 0},
            {"/input/b/touch", 0},
            {"/input/trigger/touch", 0},
            {"/input/grip/touch", 0},
            {"/input/joystick/click", 0},
            {"/input/joystick/touch", 0},
        };

        scalar_components_ = {
            {"/input/grip/value", 0},
            {"/input/trigger/value", 0},
            {"/input/joystick/x", 1},
            {"/input/joystick/y", 1}
        };
    }
}

std::string BodyTracker::get_serial() const
{
    return _serial;
}

void BodyTracker::update()
{
    if (_index != vr::k_unTrackedDeviceIndexInvalid && _activated)
    {
        // If _active is false, then disconnect the tracker
        _pose.poseIsValid = _valid;
        _pose.deviceIsConnected = _active;

        vr::VRServerDriverHost()->TrackedDevicePoseUpdated(_index, _pose, sizeof _pose);
    }
}

bool BodyTracker::set_pose(const dTrackerBase& tracker)
{
    try
    {
        // Position
        _pose.vecPosition[0] = tracker.Position.X;
        _pose.vecPosition[1] = tracker.Position.Y;
        _pose.vecPosition[2] = tracker.Position.Z;
        _valid = tracker.TrackingState;

        // Rotation
        _pose.qRotation.w = tracker.Orientation.W;
        _pose.qRotation.x = tracker.Orientation.X;
        _pose.qRotation.y = tracker.Orientation.Y;
        _pose.qRotation.z = tracker.Orientation.Z;

        // If the sender defines its own velocity
        if (tracker.Velocity.HasValue)
        {
            // Velocity
            _pose.vecVelocity[0] = tracker.Velocity.Value.X;
            _pose.vecVelocity[1] = tracker.Velocity.Value.Y;
            _pose.vecVelocity[2] = tracker.Velocity.Value.Z;
        }
        else
        {
            // Velocity
            _pose.vecVelocity[0] = 0.;
            _pose.vecVelocity[1] = 0.;
            _pose.vecVelocity[2] = 0.;
        }

        // If the sender defines its own acceleration
        if (tracker.Acceleration.HasValue)
        {
            // Acceleration
            _pose.vecAcceleration[0] = tracker.Acceleration.Value.X;
            _pose.vecAcceleration[1] = tracker.Acceleration.Value.Y;
            _pose.vecAcceleration[2] = tracker.Acceleration.Value.Z;
        }
        else
        {
            // Acceleration
            _pose.vecAcceleration[0] = 0.;
            _pose.vecAcceleration[1] = 0.;
            _pose.vecAcceleration[2] = 0.;
        }

        // If the sender defines its own ang velocity
        if (tracker.AngularVelocity.HasValue)
        {
            // Angular Velocity
            _pose.vecAngularVelocity[0] = tracker.AngularVelocity.Value.X;
            _pose.vecAngularVelocity[1] = tracker.AngularVelocity.Value.Y;
            _pose.vecAngularVelocity[2] = tracker.AngularVelocity.Value.Z;
        }
        else
        {
            // Angular Velocity
            _pose.vecAngularVelocity[0] = 0.;
            _pose.vecAngularVelocity[1] = 0.;
            _pose.vecAngularVelocity[2] = 0.;
        }

        // If the sender defines its own ang acceleration
        if (tracker.AngularAcceleration.HasValue)
        {
            // Angular Acceleration
            _pose.vecAngularAcceleration[0] = tracker.AngularAcceleration.Value.X;
            _pose.vecAngularAcceleration[1] = tracker.AngularAcceleration.Value.Y;
            _pose.vecAngularAcceleration[2] = tracker.AngularAcceleration.Value.Z;
        }
        else
        {
            // Angular Acceleration
            _pose.vecAngularAcceleration[0] = 0.;
            _pose.vecAngularAcceleration[1] = 0.;
            _pose.vecAngularAcceleration[2] = 0.;
        }
    }
    catch (...)
    {
        return false;
    }

    // All fine
    return true;
}

void BodyTracker::set_state(const bool state)
{
    _active = state;
}

bool BodyTracker::update_input(const std::string& path, const bool& value)
{
    // If the path is a well-known type of input action
    if (_type == Tracker_LeftHand && input_paths_map_left_.contains(path))
        return input_paths_map_left_[path].invoke(value);
    if (_type == Tracker_RightHand && input_paths_map_right_.contains(path))
        return input_paths_map_right_[path].invoke(value);

    if (!boolean_components_.contains(path) || boolean_components_[path] <= 0) return false;
    return vr::VRDriverInput()->UpdateBooleanComponent(
        boolean_components_[path], value, 0) == vr::VRInputError_None;
}

bool BodyTracker::update_input(const std::string& path, const float& value)
{
    // If the path is a well-known type of input action
    if (_type == Tracker_LeftHand && input_paths_map_left_.contains(path))
        return input_paths_map_left_[path].invoke(value);
    if (_type == Tracker_RightHand && input_paths_map_right_.contains(path))
        return input_paths_map_right_[path].invoke(value);

    if (!scalar_components_.contains(path) || scalar_components_[path] <= 0) return false;
    return vr::VRDriverInput()->UpdateScalarComponent(
        scalar_components_[path], value, 0) == vr::VRInputError_None;
}

bool BodyTracker::spawn()
{
    try
    {
        if (!_added && !_serial.empty())
        {
            // Add device to OpenVR devices list
            vr::VRServerDriverHost()->TrackedDeviceAdded(_serial.c_str(), vr::TrackedDeviceClass_GenericTracker, this);
            _added = true;
            return true;
        }
    }
    catch (...) // NOLINT(bugprone-empty-catch)
    {
    }
    return false;
}

vr::TrackedDeviceIndex_t BodyTracker::get_index() const
{
    return _index;
}

ITrackerType BodyTracker::get_role() const
{
    return static_cast<ITrackerType>(_role);
}

void BodyTracker::process_event(const vr::VREvent_t& event)
{
}

vr::EVRInitError BodyTracker::Activate(vr::TrackedDeviceIndex_t index)
{
    // Save the device index
    _index = index;

    // Get the properties handle for our controller
    _props = vr::VRProperties()->TrackedDeviceToPropertyContainer(_index);

    // Set our universe ID
    vr::VRProperties()->SetUint64Property(_props, vr::Prop_CurrentUniverseId_Uint64, 2);

    // Create a haptic component
    uint64_t handle_temp = 0;
    vr::VRDriverInput()->CreateHapticComponent(_props, "/output/haptic", &handle_temp);

    // Create other components
    for (auto& [component, handle] : boolean_components_)
        vr::VRDriverInput()->CreateBooleanComponent(_props, component.c_str(), &handle);
    for (auto& [component, handle] : scalar_components_)
        vr::VRDriverInput()->CreateScalarComponent(_props, component.c_str(), &handle,
                                                   vr::EVRScalarType::VRScalarType_Absolute,
                                                   static_cast<vr::EVRScalarUnits>(handle));

    // Register all properties
    vr::VRProperties()->SetStringProperty(_props, vr::Prop_TrackingSystemName_String, "amethyst");
    vr::VRProperties()->SetStringProperty(_props, vr::Prop_SerialNumber_String, _serial.c_str());

    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_WillDriftInYaw_Bool, false);
    vr::VRProperties()->SetStringProperty(_props, vr::Prop_ManufacturerName_String, is_hand() ? "Oculus" : "HTC");
    vr::VRProperties()->SetStringProperty(_props, vr::Prop_TrackingFirmwareVersion_String,
                                          "1541800000 RUNNER-WATCHMAN$runner-watchman@runner-watchman 2018-01-01 FPGA 512(2.56/0/0) BL 0 VRC 1541800000 Radio 1518800000");
    vr::VRProperties()->SetStringProperty(_props, vr::Prop_HardwareRevision_String,
                                          "product 128 rev 2.5.6 lot 2000/0/0 0");

    vr::VRProperties()->SetStringProperty(_props, vr::Prop_ConnectedWirelessDongle_String, "D0000BE000");
    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_DeviceIsWireless_Bool, true);
    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_DeviceIsCharging_Bool, false);
    vr::VRProperties()->SetFloatProperty(_props, vr::Prop_DeviceBatteryPercentage_Float, 1.f);

    vr::HmdMatrix34_t l_transform = {-1.f, 0.f, 0.f, 0.f, 0.f, 0.f, -1.f, 0.f, 0.f, -1.f, 0.f, 0.f};
    vr::VRProperties()->SetProperty(_props, vr::Prop_StatusDisplayTransform_Matrix34, &l_transform,
                                    sizeof(vr::HmdMatrix34_t),
                                    vr::k_unHmdMatrix34PropertyTag);

    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_Firmware_UpdateAvailable_Bool, false);
    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_Firmware_ManualUpdate_Bool, false);
    vr::VRProperties()->SetStringProperty(_props, vr::Prop_Firmware_ManualUpdateURL_String,
                                          "https://developer.valvesoftware.com/wiki/SteamVR/HowTo_Update_Firmware");
    vr::VRProperties()->SetUint64Property(_props, vr::Prop_HardwareRevision_Uint64, 2214720000);
    vr::VRProperties()->SetUint64Property(_props, vr::Prop_FirmwareVersion_Uint64, 1541800000);
    vr::VRProperties()->SetUint64Property(_props, vr::Prop_FPGAVersion_Uint64, 512);
    vr::VRProperties()->SetUint64Property(_props, vr::Prop_VRCVersion_Uint64, 1514800000);
    vr::VRProperties()->SetUint64Property(_props, vr::Prop_RadioVersion_Uint64, 1518800000);
    vr::VRProperties()->SetUint64Property(_props, vr::Prop_DongleVersion_Uint64, 8933539758);

    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_DeviceProvidesBatteryStatus_Bool, true);
    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_DeviceCanPowerOff_Bool, true);
    vr::VRProperties()->SetStringProperty(_props, vr::Prop_Firmware_ProgrammingTarget_String, _serial.c_str());
    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_Firmware_ForceUpdateRequired_Bool, false);

    // vr::VRProperties()->SetUint64Property(_props, vr::Prop_ParentDriver_Uint64, 8589934597);
    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_Identifiable_Bool, false);
    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_Firmware_RemindUpdate_Bool, false);
    vr::VRProperties()->SetInt32Property(_props, vr::Prop_ControllerHandSelectionPriority_Int32, -1);

    switch (_type) // NOLINT(clang-diagnostic-switch-enum)
    {
    case Tracker_LeftHand:
        vr::VRProperties()->SetInt32Property(_props, vr::Prop_ControllerRoleHint_Int32,
                                             vr::TrackedControllerRole_LeftHand);
        break;
    case Tracker_RightHand:
        vr::VRProperties()->SetInt32Property(_props, vr::Prop_ControllerRoleHint_Int32,
                                             vr::TrackedControllerRole_RightHand);
        break;
    default:
        vr::VRProperties()->SetInt32Property(_props, vr::Prop_ControllerRoleHint_Int32,
                                             vr::TrackedControllerRole_Invalid);
        break;
    }

    if (is_hand())
    {
        vr::VRProperties()->SetInt32Property(_props, vr::Prop_DeviceClass_Int32, vr::TrackedDeviceClass_Controller);
        //vr::VRProperties()->SetStringProperty(_props, vr::Prop_ResourceRoot_String, "oculus");
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_ModelNumber_String, "Miramar (Left Controller)");

        vr::VRProperties()->SetStringProperty(_props, vr::Prop_RenderModelName_String,
                                              std::format("oculus_quest2_controller_{}",
                                                          _type == Tracker_LeftHand ? "left" : "right").c_str());
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_RegisteredDeviceType_String,
                                              std::format("culus/1WMHH000X00000_Controller_{}",
                                                          _type == Tracker_LeftHand ? "Left" : "Right").c_str());

        vr::VRProperties()->SetStringProperty(_props, vr::Prop_ControllerType_String,
                                              "oculus_touch");
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_InputProfilePath_String,
                                              "{oculus}/input/touch_profile.json");

        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceReady_String,
                                              std::format("{{oculus}}/icons/rifts_{}_controller_ready.png",
                                                          _type == Tracker_LeftHand ? "left" : "right").c_str());
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceOff_String,
                                              std::format("{{oculus}}/icons/rifts_{}_controller_off.png",
                                                          _type == Tracker_LeftHand ? "left" : "right").c_str());
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceSearching_String,
                                              std::format("{{oculus}}/icons/rifts_{}_controller_searching.gif",
                                                          _type == Tracker_LeftHand ? "left" : "right").c_str());
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceSearchingAlert_String,
                                              std::format("{{oculus}}/icons/rifts_{}_controller_searching_alert.gif",
                                                          _type == Tracker_LeftHand ? "left" : "right").c_str());
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceReadyAlert_String,
                                              std::format("{{oculus}}/icons/rifts_{}_controller_ready_alert.png",
                                                          _type == Tracker_LeftHand ? "left" : "right").c_str());
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceNotReady_String,
                                              std::format("{{oculus}}/icons/rifts_{}_controller_not_ready.png",
                                                          _type == Tracker_LeftHand ? "left" : "right").c_str());
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceStandby_String,
                                              std::format("{{oculus}}/icons/rifts_{}_controller_standby.png",
                                                          _type == Tracker_LeftHand ? "left" : "right").c_str());
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceAlertLow_String,
                                              std::format("{{oculus}}/icons/rifts_{}_controller_ready_low.png",
                                                          _type == Tracker_LeftHand ? "left" : "right").c_str());

        // Propagate input components to controller actions
        for (auto& action_set : input_paths_map_left_ | std::views::values)
            action_set.update_components(boolean_components_, scalar_components_);

        for (auto& action_set : input_paths_map_right_ | std::views::values)
            action_set.update_components(boolean_components_, scalar_components_);
    }
    else
    {
        vr::VRProperties()->SetInt32Property(_props, vr::Prop_DeviceClass_Int32, vr::TrackedDeviceClass_GenericTracker);
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_RegisteredDeviceType_String,
                                              ("amethyst/vr_tracker/" + _serial).c_str());

        vr::VRProperties()->SetStringProperty(_props, vr::Prop_ResourceRoot_String, "htc");
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_ModelNumber_String, "Amethyst BodyTracker");
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_RenderModelName_String, "{htc}vr_tracker_vive_1_0");

        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceOff_String,
                                              "{htc}/icons/tracker_status_off.png");
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceSearching_String,
                                              "{htc}/icons/tracker_status_searching.gif");
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceSearchingAlert_String,
                                              "{htc}/icons/tracker_status_searching_alert.gif");
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceReady_String,
                                              "{htc}/icons/tracker_status_ready.png");
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceReadyAlert_String,
                                              "{htc}/icons/tracker_status_ready_alert.png");
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceNotReady_String,
                                              "{htc}/icons/tracker_status_error.png");
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceStandby_String,
                                              "{htc}/icons/tracker_status_standby.png");
        vr::VRProperties()->SetStringProperty(_props, vr::Prop_NamedIconPathDeviceAlertLow_String,
                                              "{htc}/icons/tracker_status_ready_low.png");

        /* Get tracker role */
        const std::string role_enum_string = ITrackerType_String.at(static_cast<ITrackerType>(_role));

        /* Update controller type and input path */
        const std::string input_path =
            "{htc}/input/tracker/" + role_enum_string + "_profile.json";

        vr::VRProperties()->SetStringProperty(_props,
                                              vr::Prop_InputProfilePath_String, input_path.c_str());
        vr::VRProperties()->SetStringProperty(_props,
                                              vr::Prop_ControllerType_String, role_enum_string.c_str());

        /* Update tracker's role in menu */
        vr::VRSettings()->SetString(vr::k_pch_Trackers_Section, ("/devices/amethyst/vr_tracker/" + _serial).c_str(),
                                    ITrackerType_Role_String.at(static_cast<ITrackerType>(_role)));
    }

    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_HasDisplayComponent_Bool, false);
    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_HasCameraComponent_Bool, false);
    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_HasDriverDirectModeComponent_Bool, false);
    vr::VRProperties()->SetBoolProperty(_props, vr::Prop_HasVirtualDisplayComponent_Bool, false);

    /* Mark tracker as activated */
    _activated = true;
    return vr::VRInitError_None;
}

vr::DriverPose_t BodyTracker::GetPose()
{
    return _pose;
}
