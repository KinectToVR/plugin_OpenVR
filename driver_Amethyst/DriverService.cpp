#include "DriverService.h"
#include <RpcProxy.h>
#include <shellapi.h>

#include "constants.hpp"
#include "Logging.h"
#include "util/color.hpp"

DWORD DriverService::proxy_stub_registration_cookie_ = 0;

DriverService::DriverService() = default;

HRESULT DriverService::GetVersion(DWORD* apiVersion) noexcept
{
    if (apiVersion)
    {
        *apiVersion = TAP_API_VERSION;
    }

    return S_OK;
}

HRESULT DriverService::SetTrackerState(dTrackerBase tracker)
{
    if (tracker_vector_ == nullptr) return E_FAIL;
    if (tracker_vector_->size() > static_cast<int>(tracker.Role))
    {
        // Create a handle to the updated (native) tracker
        const auto p_tracker = &tracker_vector_->at(tracker.Role);

        // Check the state and attempts spawning the tracker
        if (!p_tracker->is_added() && !p_tracker->spawn())
        {
            logMessage(std::format("Couldn't spawn tracker ID {} due to an unknown native exception.",
                                   static_cast<int>(tracker.Role)));
            return E_FAIL; // Failure
        }

        // Set the state of the native tracker
        p_tracker->set_state(tracker.ConnectionState);
        logMessage(std::format("Tracker ID {} state set to {}.",
                               static_cast<int>(tracker.Role), tracker.ConnectionState == 1));

        // Call the VR update handler and compose the result
        tracker_vector_->at(tracker.Role).update();
        return S_OK;
    }

    logMessage(std::format("Couldn't spawn tracker ID {}. The tracker index was out of bounds.",
                           static_cast<int>(tracker.Role)));

    return ERROR_INVALID_INDEX; // Failure
}

HRESULT DriverService::UpdateTracker(dTrackerBase tracker)
{
    if (tracker_vector_ == nullptr) return E_FAIL;
    if (tracker_vector_->size() > static_cast<int>(tracker.Role))
    {
        // Update the pose of the passed tracker
        if (!tracker_vector_->at(tracker.Role).set_pose(tracker))
        {
            logMessage(std::format("Couldn't spawn tracker ID {} due to an unknown native exception.",
                                   static_cast<int>(tracker.Role)));
            return E_FAIL; // Failure
        }

        // Call the VR update handler and compose the result
        return S_OK;
    }

    logMessage(std::format("Couldn't spawn tracker ID {}. The tracker index was out of bounds.",
                           static_cast<int>(tracker.Role)));

    return ERROR_INVALID_INDEX; // Failure
}

HRESULT DriverService::RequestVrRestart(wchar_t* message)
{
    // Sanity check
    if (message == nullptr || WStringToString(message).empty())
    {
        logMessage("Couldn't request a reboot. The reason string is empty.");
        return ERROR_EMPTY; // Compose the reply
    }

    // Perform the request
    logMessage(std::format("Requesting OpenVR restart with reason: {}", WStringToString(message)));
    vr::VRServerDriverHost()->RequestRestart(
        WStringToString(message).c_str(),
        "vrstartup.exe", "", "");

    return S_OK; // Compose the reply
}

HRESULT DriverService::PingDriverService(long long* ms)
{
    // Sanity check
    if (ms == nullptr)
    {
        logMessage("Couldn't fulfill the request. The ms pointer is empty.");
        return ERROR_EMPTY; // Compose the reply
    }

    // Perform the request
    *ms = std::chrono::system_clock::now().time_since_epoch().count();

    return S_OK; // Compose the reply
}

DriverService::~DriverService()
{
    //winrt::check_hresult(RevokeActiveObject(register_cookie_, nullptr));
    CoUninitialize();
}

void DriverService::InstallProxyStub()
{
    if (!proxy_stub_registration_cookie_)
    {
        winrt::com_ptr<IUnknown> proxyStub;
        winrt::check_hresult(
            DLLGETCLASSOBJECT_ENTRY(PROXY_CLSID_IS, winrt::guid_of<decltype(proxyStub)::type>(),
                                    proxyStub.put_void()));
        winrt::check_hresult(CoRegisterClassObject(
            PROXY_CLSID_IS, proxyStub.get(), CLSCTX_INPROC_SERVER, REGCLS_MULTIPLEUSE,
            &proxy_stub_registration_cookie_));

        winrt::check_hresult(CoRegisterPSClsid(IID_IDriverService, PROXY_CLSID_IS));
        winrt::check_hresult(CoRegisterPSClsid(IID_IVersionedApi, PROXY_CLSID_IS));
    }
}

void DriverService::UninstallProxyStub()
{
    if (proxy_stub_registration_cookie_)
    {
        winrt::check_hresult(CoRevokeClassObject(proxy_stub_registration_cookie_));
    }
}

void DriverService::TrackerVector(std::vector<BodyTracker>* const& vector)
{
    tracker_vector_ = vector;
}

void DriverService::RebuildCallback(IRebuildCallback* callback)
{
    rebuild_callback_ = callback;
}

ULONG DriverService::Release() noexcept
{
    const auto count = implements::Release();
    logMessage(std::format("COM ref released, running total: {}", count));

    if (count == 1 && rebuild_callback_)
    {
        logMessage("Client disconnected");
        logMessage("COM revocation detected, requesting rebuild!");
        rebuild_callback_->OnRebuildRequested();
        rebuild_callback_ = nullptr; // Clear the callback
    }

    return count;
}
