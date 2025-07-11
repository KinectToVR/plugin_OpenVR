#pragma once
#include "DriverService.h"
#include <openvr_driver.h>

#include <set>
#include <map>
#include <semaphore>

class ServerProvider : public vr::IServerTrackedDeviceProvider, IRebuildCallback
{
private:
    winrt::com_ptr<DriverService> driver_service_ = nullptr;
    std::map<ITrackerType, BodyTracker> tracker_vector_ = {};
    std::map<uint32_t, dDriverPose> pose_overrides_;

    std::counting_semaphore<1> driver_semaphore_{0};
    DWORD register_cookie_ = 0;

public:
    ServerProvider() = default;

    vr::EVRInitError Init(vr::IVRDriverContext* pDriverContext) override;

    void SetupService(_GUID clsid = CLSID_DriverService);

    void OnRebuildRequested() override;

    void DriverCleanup();

    void Cleanup() override;

    const char* const* GetInterfaceVersions() override;

    // It's running every frame
    void RunFrame() override;

    bool ShouldBlockStandbyMode() override;

    void EnterStandby() override;

    void LeaveStandby() override;

    bool HandleDevicePoseUpdated(uint32_t openVRID, vr::DriverPose_t& pose);

    void SetPoseOverride(uint32_t id, bool isEnabled);

    void UpdateDriverPose(uint32_t id, dDriverPose pose);
};
