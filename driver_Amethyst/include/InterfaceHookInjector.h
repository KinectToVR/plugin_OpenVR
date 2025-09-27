#pragma once
#if USE_HOOKS
#include <openvr_driver.h>

namespace amethyst::driver::implementation
{
    class ServerProvider;
}

static void DetourTrackedDevicePoseUpdated(vr::IVRServerDriverHost* _this,
                                           uint32_t unWhichDevice, const vr::DriverPose_t& newPose,
                                           uint32_t unPoseStructSize);

void InjectHooks(amethyst::driver::implementation::ServerProvider* driver, vr::IVRDriverContext* pDriverContext);
void DisableHooks();
#endif
