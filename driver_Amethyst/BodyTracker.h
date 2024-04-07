#pragma once
#include <filesystem>
#include <map>
#include <openvr_driver.h>

#include "DataContract.h"

#define AME_API_GET_TIMESTAMP_NOW \
	std::chrono::time_point_cast<std::chrono::microseconds>	\
	(std::chrono::system_clock::now()).time_since_epoch().count()

enum ITrackerType : int
{
    Tracker_Handed = 0,
    Tracker_LeftFoot = 1,
    Tracker_RightFoot = 2,
    Tracker_LeftShoulder = 3,
    Tracker_RightShoulder = 4,
    Tracker_LeftElbow = 5,
    Tracker_RightElbow = 6,
    Tracker_LeftKnee = 7,
    Tracker_RightKnee = 8,
    Tracker_Waist = 9,
    Tracker_Chest = 10,
    Tracker_Camera = 11,
    Tracker_Keyboard = 12
};

// Mapping enum to string for eliminating if-else loop
const std::map<ITrackerType, const char*>
ITrackerType_String{
    {Tracker_Handed, "vive_tracker_handed"},
    {Tracker_LeftFoot, "vive_tracker_left_foot"},
    {Tracker_RightFoot, "vive_tracker_right_foot"},
    {Tracker_LeftShoulder, "vive_tracker_left_Shoulder"},
    {Tracker_RightShoulder, "vive_tracker_right_shoulder"},
    {Tracker_LeftElbow, "vive_tracker_left_elbow"},
    {Tracker_RightElbow, "vive_tracker_right_elbow"},
    {Tracker_LeftKnee, "vive_tracker_left_knee"},
    {Tracker_RightKnee, "vive_tracker_right_knee"},
    {Tracker_Waist, "vive_tracker_waist"},
    {Tracker_Chest, "vive_tracker_chest"},
    {Tracker_Camera, "vive_tracker_camera"},
    {Tracker_Keyboard, "vive_tracker_keyboard"}
},

ITrackerType_Role_String{
    {Tracker_Handed, "TrackerRole_Handed"},
    {Tracker_LeftFoot, "TrackerRole_LeftFoot"},
    {Tracker_RightFoot, "TrackerRole_RightFoot"},
    {Tracker_LeftShoulder, "TrackerRole_LeftShoulder"},
    {Tracker_RightShoulder, "TrackerRole_RightShoulder"},
    {Tracker_LeftElbow, "TrackerRole_LeftElbow"},
    {Tracker_RightElbow, "TrackerRole_RightElbow"},
    {Tracker_LeftKnee, "TrackerRole_LeftKnee"},
    {Tracker_RightKnee, "TrackerRole_RightKnee"},
    {Tracker_Waist, "TrackerRole_Waist"},
    {Tracker_Chest, "TrackerRole_Chest"},
    {Tracker_Camera, "TrackerRole_Camera"},
    {Tracker_Keyboard, "TrackerRole_Keyboard"}
},

ITrackerType_Role_Serial{
    {Tracker_Handed, "AME-HANDED"},
    {Tracker_LeftFoot, "AME-LFOOT"},
    {Tracker_RightFoot, "AME-RFOOT"},
    {Tracker_LeftShoulder, "AME-LSHOULDER"},
    {Tracker_RightShoulder, "AME-RSHOULDER"},
    {Tracker_LeftElbow, "AME-LELBOW"},
    {Tracker_RightElbow, "AME-RELBOW"},
    {Tracker_LeftKnee, "AME-LKNEE"},
    {Tracker_RightKnee, "AME-RKNEE"},
    {Tracker_Waist, "AME-WAIST"},
    {Tracker_Chest, "AME-CHEST"},
    {Tracker_Camera, "AME-CAMERA"},
    {Tracker_Keyboard, "AME-KEYBOARD"}
};

class BodyTracker : public vr::ITrackedDeviceServerDriver
{
public:
    explicit BodyTracker(const std::string& serial, ITrackerType role);
    virtual ~BodyTracker() = default;

    /**
     * \brief Get tracker serial number
     * \return Returns tracker's serial in std::string
     */
    [[nodiscard]] std::string get_serial() const;

    /**
     * \brief Get device index in OpenVR
     * \return OpenVR device index in uint32_t
     */
    [[nodiscard]] vr::TrackedDeviceIndex_t get_index() const;

    /**
     * \brief Get device role in K2 / OVR
     * \return K2 tracker type / role
     */
    [[nodiscard]] ITrackerType get_role() const;

    /**
     * \brief Update void for server driver
     */
    void update();

    /**
     * \brief Function processing OpenVR events
     */
    static void process_event(const vr::VREvent_t& event);

    /**
     * \brief Activate device (called from OpenVR)
     * \return InitError for OpenVR if we're set up correctly
     */
    vr::EVRInitError Activate(vr::TrackedDeviceIndex_t index) override;

    /**
     * \brief Deactivate tracker (remove)
     */
    void Deactivate() override
    {
        // Clear device id
        _index = vr::k_unTrackedDeviceIndexInvalid;
    }

    /**
     * \brief Handle debug request (not needed/implemented)
     */
    void DebugRequest(const char* request, char* response_buffer, uint32_t response_buffer_size) override
    {
        // No custom debug requests defined
        if (response_buffer_size >= 1)
            response_buffer[0] = 0;
    }

    void EnterStandby() override
    {
    }

    virtual void LeaveStandby()
    {
    }

    virtual bool ShouldBlockStandbyMode() { return false; }

    /**
     * \brief Get component handle (for OpenVR)
     */
    void* GetComponent(const char* component) override
    {
        // No extra components on this device so always return nullptr
        return nullptr;
    }

    /**
     * \brief Return device's actual pose
     */
    vr::DriverPose_t GetPose() override;

    // Update pose
    bool set_pose(dTrackerBase const& tracker);

    void set_state(bool state);
    bool spawn(); // TrackedDeviceAdded

    // Get to know if tracker is activated (added)
    [[nodiscard]] bool is_added() const { return _added; }
    // Get to know if tracker is active (connected)
    [[nodiscard]] bool is_active() const { return _active; }

private:
    // Is tracker added/active
    bool _added = false, _active = false;
    bool _activated = false;

    // Stores the openvr supplied device index.
    vr::TrackedDeviceIndex_t _index;

    // Stores the devices current pose.
    vr::DriverPose_t _pose;

    // An identifier for OpenVR for when we want to make property changes to this device.
    vr::PropertyContainerHandle_t _props;

    // A struct for concise storage of all of the component handles for this device.
    struct TrackerComponents
    {
        vr::VRInputComponentHandle_t
            _system_click,
            _haptic;
    };

    TrackerComponents _components;
    std::string _serial;
    int _role;
};
