#include "ServerProvider.h"

#include <shellapi.h>
#include <filesystem>
#include <iostream>

#include "BodyTracker.h"
#include "InterfaceHookInjector.h"
#include "Logging.h"
#include <ranges>

// Wide String to UTF8 String
inline std::string WStringToString(const std::wstring& w_str)
{
    const int count = WideCharToMultiByte(CP_UTF8, 0, w_str.c_str(), w_str.length(), nullptr, 0, nullptr, nullptr);
    std::string str(count, 0);
    WideCharToMultiByte(CP_UTF8, 0, w_str.c_str(), -1, str.data(), count, nullptr, nullptr);
    return str;
}

ServerProvider::~ServerProvider() = default;

ServerProvider::ServerProvider(): driver_service_(winrt::make_self<DriverService>())
{
}

vr::EVRInitError ServerProvider::Init(vr::IVRDriverContext* pDriverContext)
{
    // Use the driver context (sets up a big set of globals)
    VR_INIT_SERVER_DRIVER_CONTEXT(pDriverContext)

    logMessage("Injecting server driver hooks...");
    InjectHooks(this, pDriverContext);

    logMessage("Registering driver service handlers: pose handler...");
    driver_service_.get()->RegisterDriverPoseHandler(
        [&, this](unsigned int id, dDriverPose pose) -> HRESULT
        {
            try
            {
                UpdateDriverPose(id, pose);
            }
            catch (const winrt::hresult_error& e)
            {
                logMessage(std::format("Could not update pose override for ID {}. Exception: {}", id, WStringToString(e.message().c_str())));
                return e.code().value;
            }
            catch (const std::exception& e)
            {
                logMessage(std::format("Could not update pose override for ID {}. Exception: {}", id, e.what()));
                return E_FAIL;
            }
            return S_OK;
        });

    logMessage("Registering driver service handlers: override handler...");
    driver_service_.get()->RegisterOverrideSetHandler(
        [&, this](unsigned int id, bool isEnabled) -> HRESULT
        {
            try
            {
                SetPoseOverride(id, isEnabled);
            }
            catch (const winrt::hresult_error& e)
            {
                logMessage(std::format("Could not update pose override for ID {}. Exception: {}", id, WStringToString(e.message().c_str())));
                return e.code().value;
            }
            catch (const std::exception& e)
            {
                logMessage(std::format("Could not toggle pose override for ID {}. Exception: {}", id, e.what()));
                return E_FAIL;
            }
            return S_OK;
        });

    // Append default trackers
    logMessage("Adding default trackers...");

    // Add 1 tracker for each role
    for (uint32_t role = 0; role <= static_cast<int>(Tracker_RightHand); role++)
    {
        if (role == TrackerHead) continue; // Skip unsupported roles
        driver_service_.get()->AddTracker(
            ITrackerType_Role_Serial.at(static_cast<ITrackerType>(role)), static_cast<ITrackerType>(role));
    }

    // Log the prepended trackers
    for (auto& tracker : driver_service_.get()->TrackerVector() | std::views::values)
        logMessage(std::format("Registered a tracker: ({})", tracker.get_serial()));

    logMessage("Setting up the server runner...");
    if (const auto hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED); hr != S_OK)
    {
        logMessage(std::format("Could not set up the driver service! HRESULT error: {}, {}",
                               hr, WStringToString(winrt::impl::message_from_hresult(hr).c_str())));

        return vr::VRInitError_Driver_Failed;
    }
    if (const auto hr = driver_service_.get()->SetupService(CLSID_DriverService); hr.has_value())
    {
        logMessage(std::format("Could not set up the driver service! HRESULT error: {}, {}",
                               hr.value().code().value, WStringToString(hr.value().message().c_str())));

        return vr::VRInitError_Driver_Failed;
    }

    // That's all, mark as okay
    return vr::VRInitError_None;
}

void ServerProvider::Cleanup()
{
    logMessage("Disabling server driver hooks...");
    DisableHooks();
}

const char* const* ServerProvider::GetInterfaceVersions()
{
    return vr::k_InterfaceVersions;
}

void ServerProvider::RunFrame()
{
    driver_service_.get()->UpdateTrackers();
}

bool ServerProvider::ShouldBlockStandbyMode()
{
    return false;
}

void ServerProvider::EnterStandby()
{
}

void ServerProvider::LeaveStandby()
{
}

bool ServerProvider::HandleDevicePoseUpdated(uint32_t openVRID, vr::DriverPose_t& pose)
{
    // Apply pose overrides for selected IDs
    if (pose_overrides_.contains(openVRID))
    {
        pose.qRotation.w = pose_overrides_[openVRID].Orientation.W;
        pose.qRotation.x = pose_overrides_[openVRID].Orientation.X;
        pose.qRotation.y = pose_overrides_[openVRID].Orientation.Y;
        pose.qRotation.z = pose_overrides_[openVRID].Orientation.Z;

        pose.vecPosition[0] = pose_overrides_[openVRID].Position.X;
        pose.vecPosition[1] = pose_overrides_[openVRID].Position.Y;
        pose.vecPosition[2] = pose_overrides_[openVRID].Position.Z;

        pose.poseIsValid = pose_overrides_[openVRID].TrackingState;
        pose.deviceIsConnected = pose_overrides_[openVRID].ConnectionState;
    }

    return true;
}

void ServerProvider::SetPoseOverride(uint32_t id, bool isEnabled)
{
    if (isEnabled) pose_overrides_[id] = dDriverPose();
    else pose_overrides_.erase(id);
    if (id == 0) m_is_head_override_active = isEnabled;
}

void ServerProvider::UpdateDriverPose(uint32_t id, dDriverPose pose)
{
    if (pose_overrides_.contains(id))
        pose_overrides_[id] = pose;
}

class DriverWatchdog : public vr::IVRWatchdogProvider
{
public:
    DriverWatchdog() = default;
    virtual ~DriverWatchdog() = default;

    vr::EVRInitError Init(vr::IVRDriverContext* pDriverContext) override
    {
        VR_INIT_WATCHDOG_DRIVER_CONTEXT(pDriverContext);
        return vr::VRInitError_None;
    }

    void Cleanup() override
    {
    }
};

extern "C" __declspec(dllexport) void* HmdDriverFactory(const char* pInterfaceName, int* pReturnCode)
{
    static ServerProvider k2_server_provider;
    static DriverWatchdog k2_watchdog_driver;

    if (0 == strcmp(vr::IServerTrackedDeviceProvider_Version, pInterfaceName))
    {
        return &k2_server_provider;
    }
    if (0 == strcmp(vr::IVRWatchdogProvider_Version, pInterfaceName))
    {
        return &k2_watchdog_driver;
    }

    (*pReturnCode) = vr::VRInitError_None;

    if (pReturnCode)
        *pReturnCode = vr::VRInitError_Init_InterfaceNotFound;
}
