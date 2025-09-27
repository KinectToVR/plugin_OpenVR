#include "DriverService.h"

#include <ranges>

#include "Logging.h"

namespace amethyst::driver::implementation
{
    DriverService::DriverService() = default;

    int DriverService::GetVersion(int* apiVersion) noexcept
    {
        if (apiVersion)
        {
            *apiVersion = 2;
        }

        return 0;
    }

    long DriverService::SetTrackerState(TrackerBase::Reader tracker)
    {
        if (tracker_vector_ == nullptr) return 1;

        // HMD pose override
        if (tracker.getRole() == TrackerType::TRACKER_HEAD)
            return EnableOverride(0, tracker.getConnectionState());

        // Normal case
        if (tracker_vector_->contains(static_cast<ITrackerType>(tracker.getRole())))
        {
            // Create a handle to the updated (native) tracker
            const auto p_tracker = &tracker_vector_->at(static_cast<ITrackerType>(tracker.getRole()));

            // Check the state and attempts spawning the tracker
            if (!p_tracker->is_added() && !p_tracker->spawn())
            {
                logMessage(std::format("Couldn't spawn tracker  ID {} due to an unknown native exception.",
                                       static_cast<int>(tracker.getRole())));
                return 1; // Failure
            }

            // Set the state of the native tracker
            p_tracker->set_state(tracker.getConnectionState());
            logMessage(std::format("Tracker ID {} state set to {}.",
                                   static_cast<int>(tracker.getRole()), tracker.getConnectionState() == 1));

            // Call the VR update handler and compose the result
            tracker_vector_->at(static_cast<ITrackerType>(tracker.getRole())).update();
            return 0;
        }

        logMessage(std::format("Couldn't spawn tracker ID {}. The tracker index was out of bounds.",
                               static_cast<int>(tracker.getRole())));

        return 1413L; // Failure
    }

    long DriverService::UpdateTracker(TrackerBase::Reader tracker)
    {
        if (tracker_vector_ == nullptr) return 1;

        // HMD pose override
        if (tracker.getRole() == TrackerType::TRACKER_HEAD)
        {
            auto pose = vr::DriverPose_t();

            pose.qRotation.w = tracker.getOrientation().getW();
            pose.qRotation.x = tracker.getOrientation().getX();
            pose.qRotation.y = tracker.getOrientation().getY();
            pose.qRotation.z = tracker.getOrientation().getZ();

            pose.vecPosition[0] = tracker.getPosition().getX();
            pose.vecPosition[1] = tracker.getPosition().getY();
            pose.vecPosition[2] = tracker.getPosition().getZ();

            pose.poseIsValid = true;
            pose.deviceIsConnected = true;

            return SetDriverPose(0, std::move(pose));
        }

        // Normal case
        if (tracker_vector_->contains(static_cast<ITrackerType>(tracker.getRole())))
        {
            // Update the pose of the passed tracker
            if (!tracker_vector_->at(static_cast<ITrackerType>(tracker.getRole())).set_pose(tracker))
            {
                logMessage(std::format("Couldn't spawn tracker ID {} due to an unknown native exception.",
                                       static_cast<int>(tracker.getRole())));
                return 1; // Failure
            }

            // Call the VR update handler and compose the result
            return 0;
        }

        logMessage(std::format("Couldn't spawn tracker ID {}. The tracker index was out of bounds.",
                               static_cast<int>(tracker.getRole())));

        return 1413L; // Failure
    }

    long DriverService::RequestVrRestart(std::wstring const& message)
    {
        // Sanity check
        if (message.empty() || WStringToString(message).empty())
        {
            logMessage("Couldn't request a reboot. The reason string is empty.");
            return 4306L; // Compose the reply
        }

        // Perform the request
        logMessage(std::format("Requesting OpenVR restart with reason: {}", WStringToString(message)));
        vr::VRServerDriverHost()->RequestRestart(
            WStringToString(message).c_str(),
            "vrstartup.exe", "", "");

        return 0; // Compose the reply
    }

    long DriverService::PingDriverService(long long* ms)
    {
        // Sanity check
        if (ms == nullptr)
        {
            logMessage("Couldn't fulfill the request. The ms pointer is empty.");
            return 4306L; // Compose the reply
        }

        // Perform the request
        *ms = std::chrono::system_clock::now().time_since_epoch().count();

        return 0; // Compose the reply
    }

    long DriverService::SetDriverPose(unsigned int id, DriverPose::Reader tracker)
    {
        auto pose = vr::DriverPose_t();

        pose.qRotation.w = tracker.getOrientation().getW();
        pose.qRotation.x = tracker.getOrientation().getX();
        pose.qRotation.y = tracker.getOrientation().getY();
        pose.qRotation.z = tracker.getOrientation().getZ();

        pose.vecPosition[0] = tracker.getPosition().getX();
        pose.vecPosition[1] = tracker.getPosition().getY();
        pose.vecPosition[2] = tracker.getPosition().getZ();

        pose.poseIsValid = true;
        pose.deviceIsConnected = true;

        return SetDriverPose(0, std::move(pose));
    }

    long DriverService::SetDriverPose(unsigned int id, vr::DriverPose_t pose)
    {
        if (pose_update_handler_) return pose_update_handler_(id, pose);
        return static_cast<long>(0x80004001L); // Not available
    }

    long DriverService::EnableOverride(unsigned int id, bool isEnabled)
    {
        if (override_set_handler_) return override_set_handler_(id, isEnabled);
        return 0x80004001L; // Not available
    }

    void DriverService::RegisterDriverPoseHandler(
        const std::function<long(const uint32_t& id, vr::DriverPose_t pose)>& handler)
    {
        pose_update_handler_ = handler;
        logMessage("Registered a pose update handler for DriverService");
    }

    void DriverService::RegisterOverrideSetHandler(
        const std::function<long(const uint32_t& id, bool isEnabled)>& handler)
    {
        override_set_handler_ = handler;
        logMessage("Registered an override set handler for DriverService");
    }

    long DriverService::UpdateInputBoolean(TrackerType tracker, std::wstring const& path, bool value)
    {
        if (path.empty() || WStringToString(path).empty())
        {
            logMessage("Couldn't update an input component. The path string is empty.");
            return 4306L; // Compose the reply
        }

        if (tracker_vector_->contains(static_cast<ITrackerType>(tracker)))
            return tracker_vector_->at(static_cast<ITrackerType>(tracker))
                                  .update_input(WStringToString(path), static_cast<bool>(value))
                       ? 0
                       : 12L;

        return 1413L; // Not available
    }

    long DriverService::UpdateInputScalar(TrackerType tracker, std::wstring const& path, float value)
    {
        if (path.empty() || WStringToString(path).empty())
        {
            logMessage("Couldn't update an input component. The path string is empty.");
            return 4306L; // Compose the reply
        }

        if (tracker_vector_->contains(static_cast<ITrackerType>(tracker)))
            return tracker_vector_->at(static_cast<ITrackerType>(tracker))
                                  .update_input(WStringToString(path), value)
                       ? 0
                       : 12L;

        return 1413L; // Not available
    }

    DriverService::~DriverService()
    {
        //winrt::check_int(RevokeActiveObject(register_cookie_, nullptr));
        //CoUninitialize();
    }

    void DriverService::TrackerVector(std::map<ITrackerType, BodyTracker>* const& vector)
    {
        tracker_vector_ = vector;
    }

    void DriverService::RebuildCallback(IRebuildCallback* callback)
    {
        rebuild_callback_ = callback;
    }
}
