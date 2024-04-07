#pragma once
#include <RpcProxy.h>
#include <unordered_map>
#include <xamlOM.h>
#include "winrt.hpp"
#include "undefgetcurrenttime.h"
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.Xaml.Media.h>
#include <winrt/Windows.UI.Xaml.Shapes.h>
#include "redefgetcurrenttime.h"
#include <wil/resource.h>

#include "BodyTracker.h"
#include "driver_Amethyst.h"
#include "wilx.hpp"

extern "C" {
_Check_return_ HRESULT STDAPICALLTYPE DLLGETCLASSOBJECT_ENTRY(
    _In_ REFCLSID rclsid, _In_ REFIID riid, _Outptr_ void** ppv);
}

class DriverService : public winrt::implements<
        DriverService, IDriverService, IVersionedApi, winrt::non_agile>
{
public:
    DriverService();

    DriverService(const DriverService&) = delete;
    DriverService& operator=(const DriverService&) = delete;

    DriverService(DriverService&&) = delete;
    DriverService& operator=(DriverService&&) = delete;

    HRESULT STDMETHODCALLTYPE GetVersion(DWORD* apiVersion) noexcept override;

    HRESULT STDMETHODCALLTYPE SetTrackerState(dTrackerBase tracker) override;
    HRESULT STDMETHODCALLTYPE UpdateTracker(dTrackerBase tracker) override;

    HRESULT STDMETHODCALLTYPE RequestVrRestart(wchar_t* message) override;
    HRESULT STDMETHODCALLTYPE PingDriverService(__int64* ms) override;

    ~DriverService() override;

    static void InstallProxyStub();
    static void UninstallProxyStub();

    std::optional<winrt::hresult_error> SetupService(_GUID clsid);

    void UpdateTrackers();
    void AddTracker(const std::string& serial, const ITrackerType role);
    std::vector<BodyTracker> TrackerVector();

private:
    DWORD register_cookie_;
    std::vector<BodyTracker> tracker_vector_;

    static DWORD proxy_stub_registration_cookie_;
};

extern "C"
#ifdef driver_Amethyst_EXPORTS
inline __declspec(dllexport)
#else
inline __declspec(dllimport)
#endif
HRESULT InstallProxyStub()
{
    try
    {
        DriverService::InstallProxyStub();
    }
    catch (const winrt::hresult_error& e)
    {
        return e.code();
    }
    catch (...)
    {
        return -1;
    }

    return 0;
}

extern "C"
#ifdef driver_Amethyst_EXPORTS
inline __declspec(dllexport)
#else
inline __declspec(dllimport)
#endif
HRESULT UninstallProxyStub()
{
    try
    {
        DriverService::UninstallProxyStub();
    }
    catch (const winrt::hresult_error& e)
    {
        return e.code();
    }
    catch (...)
    {
        return -1;
    }

    return 0;
}
