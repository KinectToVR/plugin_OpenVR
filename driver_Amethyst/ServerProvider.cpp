#include "pch.h"
#include <msclr/marshal_cppstd.h>
#include "DriverService.h"

#pragma unmanaged
#define _WIN32_WINNT _WIN32_WINNT_VISTA
#include <Windows.h>
#include <shellapi.h>

#include <filesystem>
#include <iostream>
#include <thread>

#include <openvr_driver.h>

namespace ktvr
{
    // Interface Version
    static const char* IAME_API_Version = "IAME_API_Version_020";

    // Get file location in AppData
    inline std::wstring GetK2AppDataFileDir(const std::wstring& relativeFilePath)
    {
        std::filesystem::create_directories(
            std::wstring(_wgetenv(L"APPDATA")) + L"\\Amethyst\\");

        return std::wstring(_wgetenv(L"APPDATA")) +
            L"\\Amethyst\\" + relativeFilePath;
    }

    // Get file location in AppData
    inline std::wstring GetK2AppDataLogFileDir(
        const std::wstring& relativeFolderName,
        const std::wstring& relativeFilePath)
    {
        std::filesystem::create_directories(
            std::wstring(_wgetenv(L"APPDATA")) +
            L"\\Amethyst\\logs\\" + relativeFolderName + L"\\");

        return std::wstring(_wgetenv(L"APPDATA")) +
            L"\\Amethyst\\logs\\" + relativeFolderName + L"\\" + relativeFilePath;
    }

    // https://stackoverflow.com/a/59617138

    // String to Wide String (The better one)
    inline std::wstring StringToWString(const std::string& str)
    {
        const int count = MultiByteToWideChar(CP_UTF8, 0, str.c_str(), str.length(), nullptr, 0);
        std::wstring w_str(count, 0);
        MultiByteToWideChar(CP_UTF8, 0, str.c_str(), str.length(), w_str.data(), count);
        return w_str;
    }

    // Wide String to UTF8 String (The cursed one)
    inline std::string WStringToString(const std::wstring& w_str)
    {
        const int count = WideCharToMultiByte(CP_UTF8, 0, w_str.c_str(), w_str.length(), nullptr, 0, nullptr, nullptr);
        std::string str(count, 0);
        WideCharToMultiByte(CP_UTF8, 0, w_str.c_str(), -1, str.data(), count, nullptr, nullptr);
        return str;
    }
}

#pragma managed
class K2ServerProvider : public vr::IServerTrackedDeviceProvider
{
public:
    K2ServerProvider()
    {
    }

    vr::EVRInitError Init(vr::IVRDriverContext* pDriverContext) override
    {
        // Use the driver context (sets up a big set of globals)
        VR_INIT_SERVER_DRIVER_CONTEXT(pDriverContext)

        // Initialize logging
        server::service->InitLogging();

        // Append default trackers
        server::service->LogInfo(L"Adding default trackers...");

        // Add 1 tracker for each role
        for (uint32_t role = 0; role <= static_cast<int>(Tracker_Keyboard); role++)
            tracker_vector_.emplace_back(
                ITrackerType_Role_Serial.at(static_cast<ITrackerType>(role)), static_cast<ITrackerType>(role));

        // Log the prepended trackers
        for (auto& tracker : tracker_vector_)
            server::service->LogInfo(L"Registered a tracker: " + gcnew System::String(tracker.get_serial().c_str()));

        server::service->LogInfo(L"Setting up the server runner...");
        server::service->SetupRunner(L"E8F6C6A4-9911-4541-A5F5-7DAAE97ADDAF");

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

class K2WatchdogDriver : public vr::IVRWatchdogProvider
{
public:
    K2WatchdogDriver() = default;
    virtual ~K2WatchdogDriver() = default;

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
    // ktvr::GetK2AppDataFileDir will create all directories by itself

    // Set up the logging directory
    const auto thisLogDestination =
        ktvr::GetK2AppDataLogFileDir(L"VRDriver", L"Amethyst_VRDriver_");

    static K2ServerProvider k2_server_provider;
    static K2WatchdogDriver k2_watchdog_driver;

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
