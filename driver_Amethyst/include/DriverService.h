#pragma once
#include <unordered_map>
#include <functional>

#include "BodyTracker.h"
#include "Logging.h"

namespace amethyst::driver::implementation
{
    struct IRebuildCallback
    {
        virtual void OnRebuildRequested() = 0;
        virtual ~IRebuildCallback() = default;
    };

    class DriverService
    {
    public:
        DriverService();
        ~DriverService();

        DriverService(const DriverService&) = delete;
        DriverService& operator=(const DriverService&) = delete;

        DriverService(DriverService&&) = delete;
        DriverService& operator=(DriverService&&) = delete;

        int GetVersion(int* apiVersion) noexcept;

        long SetTrackerState(TrackerBase::Reader tracker);
        long UpdateTracker(TrackerBase::Reader tracker);

        long RequestVrRestart(std::wstring const& message);
        long PingDriverService(long long* ms);

        // Note: sending a "Head" tracker is the same as SetDriverPose(0, ...)
        long SetDriverPose(unsigned int id, DriverPose::Reader pose);
        long SetDriverPose(unsigned int id, vr::DriverPose_t pose);
        long EnableOverride(unsigned int id, bool isEnabled);

        long UpdateInputBoolean(TrackerType tracker, std::wstring const& path, bool value);
        long UpdateInputScalar(TrackerType tracker, std::wstring const& path, float value);

        void TrackerVector(std::map<ITrackerType, BodyTracker>* const& vector);
        void RebuildCallback(IRebuildCallback* callback);

        void RegisterDriverPoseHandler(const std::function<long(const uint32_t& id, vr::DriverPose_t pose)>& handler);
        void RegisterOverrideSetHandler(const std::function<long(const uint32_t& id, bool isEnabled)>& handler);

    private:
        IRebuildCallback* rebuild_callback_ = nullptr;
        std::map<ITrackerType, BodyTracker>* tracker_vector_;

        std::function<long(const uint32_t& id, vr::DriverPose_t pose)> pose_update_handler_;
        std::function<long(const uint32_t& id, bool isEnabled)> override_set_handler_;
    };
}
