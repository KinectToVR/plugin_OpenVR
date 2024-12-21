#pragma once
#include "DriverService.h"
#include <openvr_driver.h>

#include <set>
#include <map>

class ServerProvider : public vr::IServerTrackedDeviceProvider
{
private:
    winrt::com_ptr<DriverService> driver_service_;
    std::map<uint32_t, dDriverPose> pose_overrides_;

public:
    virtual ~ServerProvider();

    ServerProvider();

    vr::EVRInitError Init(vr::IVRDriverContext* pDriverContext) override;

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