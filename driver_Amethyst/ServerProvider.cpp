#include "DriverService.h"

#include <shellapi.h>

#include <filesystem>
#include <iostream>

#include <openvr_driver.h>

#include "BodyTracker.h"
#include "Logging.h"

// Wide String to UTF8 String
inline std::string WStringToString(const std::wstring& w_str)
{
    const int count = WideCharToMultiByte(CP_UTF8, 0, w_str.c_str(), w_str.length(), nullptr, 0, nullptr, nullptr);
    std::string str(count, 0);
    WideCharToMultiByte(CP_UTF8, 0, w_str.c_str(), -1, str.data(), count, nullptr, nullptr);
    return str;
}


class ServerProvider : public vr::IServerTrackedDeviceProvider
{
private:
    winrt::com_ptr<DriverService> driver_service_;

public:
    virtual ~ServerProvider() = default;

    ServerProvider() : driver_service_(winrt::make_self<DriverService>()) // NOLINT(modernize-use-equals-default)
    {
    }

    vr::EVRInitError Init(vr::IVRDriverContext* pDriverContext) override
    {
        // Use the driver context (sets up a big set of globals)
        VR_INIT_SERVER_DRIVER_CONTEXT(pDriverContext)

        // Append default trackers
        logMessage("Adding default trackers...");

        // Add 1 tracker for each role
        for (uint32_t role = 0; role <= static_cast<int>(Tracker_Keyboard); role++)
            driver_service_.get()->AddTracker(
                ITrackerType_Role_Serial.at(static_cast<ITrackerType>(role)), static_cast<ITrackerType>(role));

        // Log the prepended trackers
        for (auto& tracker : driver_service_.get()->TrackerVector())
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
        driver_service_.get()->UpdateTrackers();
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
