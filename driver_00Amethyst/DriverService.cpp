#include "DriverService.h"

#include <ranges>
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

    // HMD pose override
    if (tracker.Role == TrackerHead)
        return EnableOverride(0, tracker.ConnectionState);

    // Normal case
    if (tracker_vector_->contains(static_cast<ITrackerType>(tracker.Role)))
    {
        // Create a handle to the updated (native) tracker
        const auto p_tracker = &tracker_vector_->at(static_cast<ITrackerType>(tracker.Role));

        // Check the state and attempts spawning the tracker
        if (!p_tracker->is_added() && !p_tracker->spawn())
        {
            logMessage(std::format("Couldn't spawn tracker  ID {} due to an unknown native exception.",
                                   static_cast<int>(tracker.Role)));
            return E_FAIL; // Failure
        }

        // Set the state of the native tracker
        p_tracker->set_state(tracker.ConnectionState);
        logMessage(std::format("Tracker ID {} state set to {}.",
                               static_cast<int>(tracker.Role), tracker.ConnectionState == 1));

        // Call the VR update handler and compose the result
        tracker_vector_->at(static_cast<ITrackerType>(tracker.Role)).update();
        return S_OK;
    }

    logMessage(std::format("Couldn't spawn tracker ID {}. The tracker index was out of bounds.",
                           static_cast<int>(tracker.Role)));

    return ERROR_INVALID_INDEX; // Failure
}

HRESULT DriverService::UpdateTracker(dTrackerBase tracker)
{
    if (tracker_vector_ == nullptr) return E_FAIL;

    // HMD pose override
    if (tracker.Role == TrackerHead)
    {
        return SetDriverPose(0, dDriverPose{
                                 .ConnectionState = true,
                                 .TrackingState = true,
                                 .Position = tracker.Position,
                                 .Orientation = tracker.Orientation
                             });
    }

    // Normal case
    if (tracker_vector_->contains(static_cast<ITrackerType>(tracker.Role)))
    {
        // Update the pose of the passed tracker
        if (!tracker_vector_->at(static_cast<ITrackerType>(tracker.Role)).set_pose(tracker))
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

HRESULT DriverService::SetDriverPose(unsigned int id, dDriverPose pose)
{
    if (pose_update_handler_) return pose_update_handler_(id, pose);
    return E_NOTIMPL; // Not available
}

HRESULT DriverService::EnableOverride(unsigned int id, boolean isEnabled)
{
    if (override_set_handler_) return override_set_handler_(id, isEnabled);
    return E_NOTIMPL; // Not available
}

void DriverService::RegisterDriverPoseHandler(
    const std::function<HRESULT(const uint32_t& id, dDriverPose pose)>& handler)
{
    pose_update_handler_ = handler;
    logMessage("Registered a pose update handler for DriverService");
}

void DriverService::RegisterOverrideSetHandler(
    const std::function<HRESULT(const uint32_t& id, bool isEnabled)>& handler)
{
    override_set_handler_ = handler;
    logMessage("Registered an override set handler for DriverService");
}

HRESULT DriverService::UpdateInputBoolean(dTrackerType tracker, wchar_t* path, boolean value)
{
    if (path == nullptr || WStringToString(path).empty())
    {
        logMessage("Couldn't update an input component. The path string is empty.");
        return ERROR_EMPTY; // Compose the reply
    }

    if (tracker_vector_->contains(static_cast<ITrackerType>(tracker)))
        return tracker_vector_->at(static_cast<ITrackerType>(tracker))
                              .update_input(WStringToString(path), static_cast<bool>(value))
                   ? S_OK
                   : ERROR_INVALID_ACCESS;

    return ERROR_INVALID_INDEX; // Not available
}

HRESULT DriverService::UpdateInputScalar(dTrackerType tracker, wchar_t* path, float value)
{
    if (path == nullptr || WStringToString(path).empty())
    {
        logMessage("Couldn't update an input component. The path string is empty.");
        return ERROR_EMPTY; // Compose the reply
    }

    if (tracker_vector_->contains(static_cast<ITrackerType>(tracker)))
        return tracker_vector_->at(static_cast<ITrackerType>(tracker))
                              .update_input(WStringToString(path), value)
                   ? S_OK
                   : ERROR_INVALID_ACCESS;

    return ERROR_INVALID_INDEX; // Not available
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

void DriverService::TrackerVector(std::map<ITrackerType, BodyTracker>* const& vector)
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
