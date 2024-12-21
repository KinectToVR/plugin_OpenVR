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
#include <functional>

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

    // Note: sending a "Head" tracker is the same as SetDriverPose(0, ...)
    HRESULT STDMETHODCALLTYPE SetDriverPose(unsigned int id, dDriverPose pose) override;
    HRESULT STDMETHODCALLTYPE EnableOverride(unsigned int id, boolean isEnabled) override;

    HRESULT STDMETHODCALLTYPE UpdateInputBoolean(dTrackerType tracker, wchar_t* path, boolean value) override;
    HRESULT STDMETHODCALLTYPE UpdateInputScalar(dTrackerType tracker, wchar_t* path, float value) override;

    ~DriverService() override;

    static void InstallProxyStub();
    static void UninstallProxyStub();

    std::optional<winrt::hresult_error> SetupService(_GUID clsid);

    void UpdateTrackers();
    void AddTracker(const std::string& serial, const ITrackerType role);
    std::map<ITrackerType, BodyTracker> TrackerVector();

    void RegisterDriverPoseHandler(const std::function<HRESULT(const uint32_t& id, dDriverPose pose)>& handler);
    void RegisterOverrideSetHandler(const std::function<HRESULT(const uint32_t& id, bool isEnabled)>& handler);

private:
    DWORD register_cookie_;
    std::map<ITrackerType, BodyTracker> tracker_vector_;

    std::function<HRESULT(const uint32_t& id, dDriverPose pose)> pose_update_handler_;
    std::function<HRESULT(const uint32_t& id, bool isEnabled)> override_set_handler_;

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
