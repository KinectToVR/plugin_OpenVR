#include "DriverService.h"

#include <shellapi.h>

#include <filesystem>
#include <iostream>

#include <openvr_driver.h>
#include <semaphore>

#include "BodyTracker.h"
#include "Logging.h"

class ServerProvider : public vr::IServerTrackedDeviceProvider, IRebuildCallback
{
private:
    winrt::com_ptr<DriverService> driver_service_ = nullptr;
    std::vector<BodyTracker> tracker_vector_ = {};

    std::counting_semaphore<1> driver_semaphore_{0};
    DWORD register_cookie_ = 0;

public:
    ServerProvider() = default;

    vr::EVRInitError Init(vr::IVRDriverContext* pDriverContext) override
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
        for (uint32_t role = 0; role <= static_cast<int>(Tracker_Keyboard); role++)
            tracker_vector_.emplace_back(
                ITrackerType_Role_Serial.at(static_cast<ITrackerType>(role)), static_cast<ITrackerType>(role));

        // Log the prepended trackers
        for (auto& tracker : tracker_vector_)
            logMessage(std::format("Registered a tracker: ({})", tracker.get_serial()));

        // That's all, mark as okay
        return vr::VRInitError_None;
    }

    void SetupService(const _GUID clsid = CLSID_DriverService)
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

    void OnRebuildRequested() override
    {
        logMessage("The server driver was killed by COM. Requesting a restart...");
        vr::VRServerDriverHost()->RequestRestart(
            "Amethyst driver's COM server was revoked, please restart SteamVR to respin it. "
            "If you see this error often, please collect the logs and reach out to us! \n"
            "As a temporary fix, you can also try starting SteamVR first, and then Amethyst. "
            "We're deeply sorry! ＞﹏＜",
            "vrstartup.exe", "", "");
    }

    void DriverCleanup()
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

    void Cleanup() override
    {
    }

    const char* const* GetInterfaceVersions() override
    {
        return vr::k_InterfaceVersions;
    }

    // It's running every frame
    void RunFrame() override
    {
        for (auto& tracker : tracker_vector_)
            tracker.update(); // Update all
    }

    bool ShouldBlockStandbyMode() override
    {
        return false;
    }

    void EnterStandby() override
    {
    }

    void LeaveStandby() override
    {
    }
};

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
