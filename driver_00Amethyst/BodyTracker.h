// ReSharper disable CppClangTidyClangDiagnosticSwitchEnum
#pragma once
#include <filesystem>
#include <map>
#include <openvr_driver.h>

#include "DataContract.h"

#define AME_API_GET_TIMESTAMP_NOW \
	std::chrono::time_point_cast<std::chrono::microseconds>	\
	(std::chrono::system_clock::now()).time_since_epoch().count()

// Is HMD pose override enabled atm
inline bool m_is_head_override_active = false;

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
    Tracker_Keyboard = 12,
    //Tracker_Head = 13,
    Tracker_LeftHand = 14,
    Tracker_RightHand = 15
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
        {Tracker_Keyboard, "vive_tracker_keyboard"},
        {Tracker_LeftHand, "vive_tracker_left_hand"},
        {Tracker_RightHand, "vive_tracker_right_hand"}
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
        {Tracker_Keyboard, "TrackerRole_Keyboard"},
        {Tracker_LeftHand, "TrackerRole_LeftHand"},
        {Tracker_RightHand, "TrackerRole_RightHand"}
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
        {Tracker_Keyboard, "AME-KEYBOARD"},
        {Tracker_LeftHand, "AME-LHAND"},
        {Tracker_RightHand, "AME-RHAND"}
    };

enum InputActionHandlingMode : std::uint8_t
{
    ModeInvalid,
    ModeScalar, // Move true=1.0, false=0.0
    ModeBoolean, // Move to bool as >=0.5f
    ModeHasValue // True if .first is not 0
};

struct DataInputAction
{
    std::string path;
    InputActionHandlingMode mode = ModeInvalid;
};

class InputActionSet
{
public:
    InputActionSet() = default;

    explicit InputActionSet(const std::initializer_list<DataInputAction>& m_actions) : actions(m_actions)
    {
    }

    std::vector<DataInputAction> actions;
    std::map<std::string, vr::VRInputComponentHandle_t> boolean_components;
    std::map<std::string, vr::VRInputComponentHandle_t> scalar_components;

    void update_components(
        const std::map<std::string, vr::VRInputComponentHandle_t>& m_boolean_components,
        const std::map<std::string, vr::VRInputComponentHandle_t>& m_scalar_components)
    {
        boolean_components = m_boolean_components;
        scalar_components = m_scalar_components;
    }

    bool invoke(const bool& value)
    {
        if (actions.empty()) return false;
        auto result_value = false;

        for (const auto& [path, mode] : actions)
        {
            switch (mode)
            {
            case ModeBoolean:
                result_value &= update_boolean(path, value);
                break;
            case ModeScalar:
                result_value &= update_scalar(path, value ? 1.0f : 0.0f);
                break;
            case ModeHasValue:
                result_value &= update_boolean(path, value);
                break;
            default: break;
            }
        }

        return result_value;
    }

    bool invoke(const float& value)
    {
        if (actions.empty()) return false;
        auto result_value = false;

        for (const auto& [path, mode] : actions)
        {
            switch (mode)
            {
            case ModeBoolean:
                result_value &= update_boolean(path, value >= 0.5f);
                break;
            case ModeScalar:
                result_value &= update_scalar(path, value);
                break;
            case ModeHasValue:
                result_value &= update_boolean(path, value > 0.0f);
                break;
            default: break;
            }
        }

        return result_value;
    }

private:
    bool update_boolean(const std::string& path, const bool& value)
    {
        if (path.empty() || !boolean_components.contains(path)) return false;
        return vr::VRDriverInput()->UpdateBooleanComponent(
            boolean_components[path], value, 0) == vr::VRInputError_None;
    }

    bool update_scalar(const std::string& path, const float& value)
    {
        if (path.empty() || !scalar_components.contains(path)) return false;
        return vr::VRDriverInput()->UpdateScalarComponent(
            scalar_components[path], value, 0) == vr::VRInputError_None;
    }
};

class BodyTracker : public vr::ITrackedDeviceServerDriver
{
public:
    explicit BodyTracker(const std::string& serial = "AME-INVALID", ITrackerType role = Tracker_Handed);
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
    bool set_pose(const dTrackerBase& tracker);

    void set_state(bool state);
    bool spawn(); // TrackedDeviceAdded

    bool update_input(const std::string& path, const bool& value);
    bool update_input(const std::string& path, const float& value);

    // Get to know if tracker is activated (added)
    [[nodiscard]] bool is_added() const { return _added; }
    // Get to know if tracker is active (connected)
    [[nodiscard]] bool is_active() const { return _active; }
    // Get to know if tracker is a hand tracker (controller)
    [[nodiscard]] bool is_hand() const { return _type == Tracker_LeftHand || _type == Tracker_RightHand; }

private:
    // Is tracker added/active
    bool _added = false, _active = false, _valid = false;
    bool _activated = false;

    // Stores the openvr supplied device index.
    vr::TrackedDeviceIndex_t _index;

    // Stores the devices current pose.
    vr::DriverPose_t _pose;

    // An identifier for OpenVR for when we want to make property changes to this device.
    vr::PropertyContainerHandle_t _props;

    std::string _serial;
    int _role;

    ITrackerType _type;

    std::map<std::string, vr::VRInputComponentHandle_t> boolean_components_;
    std::map<std::string, vr::VRInputComponentHandle_t> scalar_components_;

    std::map<std::string, InputActionSet> input_paths_map_left_{
        {
            "1A3ABE96-B1B3-4ABF-9969-C87BB15B2C13", InputActionSet{
                DataInputAction{
                    .path = "/input/system/click",
                    .mode = ModeBoolean
                }
            }
        },
        {
            "54B78337-23B6-4E36-A9C8-047061FB9256", InputActionSet{
                DataInputAction{
                    .path = "/input/trigger/value",
                    .mode = ModeScalar
                },
                DataInputAction{
                    .path = "/input/trigger/touch",
                    .mode = ModeBoolean
                }
            }
        },
        {
            "36DE93FB-01DD-4DEC-ACE6-E9ADD96027B7", InputActionSet{
                DataInputAction{
                    .path = "/input/grip/value",
                    .mode = ModeScalar
                },
                DataInputAction{
                    .path = "/input/grip/touch",
                    .mode = ModeBoolean
                }
            }
        },
        {
            "DAE6AD34-B3E4-46D0-AFEE-1CACFB1387A1", InputActionSet{
                DataInputAction{
                    .path = "/input/x/click",
                    .mode = ModeBoolean
                },
                DataInputAction{
                    .path = "/input/x/touch",
                    .mode = ModeBoolean
                }
            }
        },
        {
            "130B197B-EFC9-4A3A-9D3F-91A35BB83291", InputActionSet{
                DataInputAction{
                    .path = "/input/y/click",
                    .mode = ModeBoolean
                },
                DataInputAction{
                    .path = "/input/y/touch",
                    .mode = ModeBoolean
                }
            }
        },
        {
            "5F519116-9A5C-48BA-9693-D9A3741AF0AB", InputActionSet{
                DataInputAction{
                    .path = "/input/joystick/x",
                    .mode = ModeBoolean
                },
                DataInputAction{
                    .path = "/input/joystick/touch",
                    .mode = ModeHasValue
                }
            }
        },
        {
            "FF80F249-7F8D-4FA1-AC88-B9A1F5D623CB", InputActionSet{
                DataInputAction{
                    .path = "/input/joystick/y",
                    .mode = ModeBoolean
                },
                DataInputAction{
                    .path = "/input/joystick/touch",
                    .mode = ModeHasValue
                }
            }
        },
    };

    std::map<std::string, InputActionSet> input_paths_map_right_{
        {
            "6169CB90-4997-4266-AC33-83FF3FEF16AA", InputActionSet{
                DataInputAction{
                    .path = "/input/system/click",
                    .mode = ModeBoolean
                }
            }
        },
        {
            "CC84BF86-6846-4A7D-9111-7919F22D0FA7", InputActionSet{
                DataInputAction{
                    .path = "/input/trigger/value",
                    .mode = ModeScalar
                },
                DataInputAction{
                    .path = "/input/trigger/touch",
                    .mode = ModeBoolean
                }
            }
        },
        {
            "65EAFD83-C5D6-496F-BA3C-7FB0F9FED824", InputActionSet{
                DataInputAction{
                    .path = "/input/grip/value",
                    .mode = ModeScalar
                },
                DataInputAction{
                    .path = "/input/grip/touch",
                    .mode = ModeBoolean
                }
            }
        },
        {
            "98279522-D951-4EAC-9705-71EB5A9151D0", InputActionSet{
                DataInputAction{
                    .path = "/input/a/click",
                    .mode = ModeBoolean
                },
                DataInputAction{
                    .path = "/input/a/touch",
                    .mode = ModeBoolean
                }
            }
        },
        {
            "1D7238C7-3391-44BA-B40F-5F33AEE64114", InputActionSet{
                DataInputAction{.path = "/input/b/click"},
                DataInputAction{.path = "/input/b/touch"}
            }
        },
        {
            "46CD8C05-16F6-42D5-9265-133E57E0933B", InputActionSet{
                DataInputAction{
                    .path = "/input/joystick/x",
                    .mode = ModeBoolean
                },
                DataInputAction{
                    .path = "/input/joystick/touch",
                    .mode = ModeHasValue
                }
            }
        },
        {
            "14E62950-A538-422E-B688-82CCB5B1E179", InputActionSet{
                DataInputAction{
                    .path = "/input/joystick/y",
                    .mode = ModeBoolean
                },
                DataInputAction{
                    .path = "/input/joystick/touch",
                    .mode = ModeHasValue
                }
            }
        },
    };
};
