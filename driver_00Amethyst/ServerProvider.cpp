#include "ServerProvider.h"

#include <shellapi.h>
#include <filesystem>
#include <iostream>

#include "BodyTracker.h"
#include "InterfaceHookInjector.h"
#include "Logging.h"
#include <ranges>
#include <semaphore>
#include <thread>

vr::EVRInitError ServerProvider::Init(vr::IVRDriverContext* pDriverContext)
{
    // Use the driver context (sets up a big set of globals)
    VR_INIT_SERVER_DRIVER_CONTEXT(pDriverContext)

    logMessage("Setting up the server runner...");
    SetupService();

    logMessage("Waiting for the setup to finish (<5s)...");
    if (!driver_semaphore_.try_acquire_for(std::chrono::seconds(5)))
    {
        logMessage(std::format("Timed out seting up the driver service!"));
        return vr::VRInitError_Driver_Failed;
    }

    // Append default trackers
    logMessage("Adding default trackers...");

    // Add 1 tracker for each role
    for (uint32_t role = 0; role <= static_cast<int>(Tracker_RightHand); role++)
    {
        if (role == TrackerHead) continue; // Skip unsupported roles
        tracker_vector_[static_cast<ITrackerType>(role)] = BodyTracker(
            ITrackerType_Role_Serial.at(static_cast<ITrackerType>(role)), static_cast<ITrackerType>(role));
    }

    // Log the prepended trackers
    for (auto& tracker : tracker_vector_ | std::views::values)
        logMessage(std::format("Registered a tracker: ({})", tracker.get_serial()));

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
                logMessage(std::format("Could not update pose override for ID {}. Exception: {}", id,
                    WStringToString(e.message().c_str())));
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
                logMessage(std::format("Could not update pose override for ID {}. Exception: {}", id,
                    WStringToString(e.message().c_str())));
                return e.code().value;
            }
            catch (const std::exception& e)
            {
                logMessage(std::format("Could not toggle pose override for ID {}. Exception: {}", id, e.what()));
                return E_FAIL;
            }
            return S_OK;
        });

    // That's all, mark as okay
    return vr::VRInitError_None;
}

void ServerProvider::SetupService(const _GUID clsid)
{
    std::thread([this, clsid]
    {
        try
        {
            init_apartment(winrt::apartment_type::multi_threaded);
            winrt::check_hresult(CoInitializeSecurity(
                nullptr, -1, nullptr, nullptr,
                RPC_C_AUTHN_LEVEL_PKT_PRIVACY,
                RPC_C_IMP_LEVEL_IDENTIFY,
                nullptr, EOAC_NONE, nullptr));

            DriverCleanup();
            driver_service_ = winrt::make_self<DriverService>();

            driver_service_->TrackerVector(&tracker_vector_);
            driver_service_->RebuildCallback(this);

            InstallProxyStub();

            // Lock the service object to keep it alive externally
            winrt::check_hresult(CoLockObjectExternal(
                static_cast<IDriverService*>(driver_service_.get()), TRUE, FALSE));

            // Use STRONG registration to keep it registered
            winrt::check_hresult(RegisterActiveObject(
                static_cast<IDriverService*>(driver_service_.get()),
                clsid, ACTIVEOBJECT_STRONG, &register_cookie_));

            // Sanity check: retrieve proxy to confirm registration
            winrt::com_ptr<IUnknown> service;
            winrt::check_hresult(GetActiveObject(
                clsid, nullptr, service.put()));

            // Setup done - unlock the service object
            driver_semaphore_.release();

            MSG msg;
            while (GetMessage(&msg, nullptr, 0, 0))
            {
                TranslateMessage(&msg);
                DispatchMessage(&msg);
            }

            DriverCleanup();
            winrt::uninit_apartment();
        }
        catch (const winrt::hresult_error& e)
        {
            logMessage(std::format("Driver service setup failed with HRESULT error: {}, {}",
                                   e.code().value, WStringToString(e.message().c_str())));
        }
        catch (...)
        {
            logMessage("Unknown error during driver service setup.");
        }
    }).detach();
}

void ServerProvider::OnRebuildRequested()
{
    logMessage("The server driver was killed by COM. Requesting a restart...");
    vr::VRServerDriverHost()->RequestRestart(
        "Amethyst driver's COM server was revoked, please restart SteamVR to respin it. "
        "If you see this error often, please collect the logs and reach out to us! \n"
        "As a temporary fix, you can also try starting SteamVR first, and then Amethyst. "
        "We're deeply sorry! ＞﹏＜",
        "vrstartup.exe", "", "");
}

void ServerProvider::DriverCleanup()
{
    if (driver_service_)
        CoDisconnectObject(
            static_cast<IDriverService*>(driver_service_.get()), 0);

    if (register_cookie_ != 0)
    {
        RevokeActiveObject(register_cookie_, nullptr);
        register_cookie_ = 0;
    }

    if (driver_service_)
        CoLockObjectExternal(
            static_cast<IDriverService*>(driver_service_.get()), FALSE, FALSE);
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
    for (auto& tracker : tracker_vector_ | std::views::values)
        tracker.update(); // Update all
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
        if (openVRID != 0)
        {
            pose.qRotation.w = pose_overrides_[openVRID].Orientation.W;
            pose.qRotation.x = pose_overrides_[openVRID].Orientation.X;
            pose.qRotation.y = pose_overrides_[openVRID].Orientation.Y;
            pose.qRotation.z = pose_overrides_[openVRID].Orientation.Z;
        }

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
