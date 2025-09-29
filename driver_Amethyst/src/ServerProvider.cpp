#include "ServerProvider.h"

#include <filesystem>
#include <iostream>
#include <ranges>
#include <semaphore>
#include <thread>

#include "BodyTracker.h"
#include "InterfaceHookInjector.h"
#include "Logging.h"

#include <capnp/ez-rpc.h>

namespace amethyst::driver::implementation
{
    vr::EVRInitError ServerProvider::Init(vr::IVRDriverContext* pDriverContext)
    {
        // Use the driver context (sets up a big set of globals)
        VR_INIT_SERVER_DRIVER_CONTEXT(pDriverContext)

        logMessage("Setting up the server runner...");

        uint32_t port = 1234;
        SetupService(&port); // TODO

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
            if (role == Tracker_Head) continue; // Skip unsupported roles
            tracker_vector_[static_cast<ITrackerType>(role)] = BodyTracker(
                ITrackerType_Role_Serial.at(static_cast<ITrackerType>(role)), static_cast<ITrackerType>(role));
        }

        // Log the prepended trackers
        for (auto& tracker : tracker_vector_ | std::views::values)
            logMessage(std::format("Registered a tracker: ({})", tracker.get_serial()));

#if USE_HOOKS
        logMessage("Injecting server driver hooks...");
        InjectHooks(this, pDriverContext);
#else
        logMessage("Server driver hooks disabled...");
#endif

        logMessage("Registering driver service handlers: pose handler...");
        driver_service_->RegisterDriverPoseHandler(
            [&, this](unsigned int id, vr::DriverPose_t pose) -> int
            {
                try
                {
                    UpdateDriverPose(id, pose);
                }
                catch (const std::exception& e)
                {
                    logMessage(std::format("Could not update pose override for ID {}. Exception: {}", id, e.what()));
                    return 1;
                }
                return 0;
            });

        logMessage("Registering driver service handlers: override handler...");
        driver_service_->RegisterOverrideSetHandler(
            [&, this](unsigned int id, bool isEnabled) -> int
            {
                try
                {
                    SetPoseOverride(id, isEnabled);
                }
                catch (const std::exception& e)
                {
                    logMessage(std::format("Could not toggle pose override for ID {}. Exception: {}", id, e.what()));
                    return 1;
                }
                return 0;
            });

        // That's all, mark as okay
        return vr::VRInitError_None;
    }

    void ServerProvider::SetupService(uint32_t* port)
    {
        std::thread([this, port]
        {
            try
            {
                if (port == nullptr) return;

                driver_service_ = std::make_shared<DriverService>();
                driver_service_->TrackerVector(&tracker_vector_);
                driver_service_->RebuildCallback(this);

                // Set up a server.
                capnp::EzRpcServer server(kj::heap<DriverImpl>(driver_service_), "*", *port);

                // Write the port number to stdout, in case it was chosen automatically.
                auto& waitScope = server.getWaitScope();
                *port = server.getPort().wait(waitScope);

                if (*port == 0)
                {
                    // The address format "unix:/path/to/socket" opens a unix domain socket,
                    // in which case the port will be zero.
                    logMessage("Listening on Unix socket...");
                }
                else
                {
                    logMessage(std::format("Listening on port {}...", *port));
                }

                // Setup done - unlock the service object
                driver_semaphore_.release();

                // Run forever, accepting connections and handling requests.
                kj::NEVER_DONE.wait(waitScope);

                //// First we need to set up the KJ async event loop. This should happen one
                //// per thread that needs to perform RPC.
                //auto io = kj::setupAsyncIo();

                //// Using KJ APIs, let's parse our network address and listen on it.
                //kj::Network& network = io.provider->getNetwork();
                //kj::Own<kj::NetworkAddress> addr = network.parseAddress("*", *port).wait(io.waitScope);
                //kj::Own<kj::ConnectionReceiver> listener = addr->listen();

                //// Write the port number to stdout, in case it was chosen automatically.
                //*port = listener->getPort();
                //if (*port == 0)
                //{
                //    // The address format "unix:/path/to/socket" opens a unix domain socket,
                //    // in which case the port will be zero.
                //    logMessage("Listening on Unix socket...");
                //}
                //else
                //{
                //    logMessage(std::format("Listening on port {}...", *port));
                //}

                //// Start the RPC server.
                //capnp::TwoPartyServer server(kj::heap<DriverImpl>(driver_service_));

                //// Setup done - unlock the service object
                //driver_semaphore_.release();

                //// Run forever, accepting connections and handling requests.
                //server.listen(*listener).wait(io.waitScope);
                //logMessage("Server exited.");

                //init_apartment(winrt::apartment_type::multi_threaded);
                //if (const auto& result = CoInitializeSecurity(
                //    nullptr, -1, nullptr, nullptr,
                //    RPC_C_AUTHN_LEVEL_PKT_PRIVACY, RPC_C_IMP_LEVEL_IDENTIFY,
                //    nullptr, EOAC_NONE, nullptr); FAILED(result))
                //{
                //    logMessage("Failed to initialize security! "
                //        "Amethyst's COM server may be revoked when the app disconnects.");

                //    if (result == RPC_E_TOO_LATE)
                //        logMessage("Reason: CoInitializeSecurity was already called by another driver.");
                //    else
                //        logMessage(std::format(
                //            "Reason: {}", WStringToString(winrt::hresult_error(result).message().c_str())));
                //}

                //DriverCleanup();
                //driver_service_ = winrt::make_self<DriverService>();

                //driver_service_->TrackerVector(&tracker_vector_);
                //driver_service_->RebuildCallback(this);

                //InstallProxyStub();

                //// Lock the service object to keep it alive externally
                //winrt::check_hresult(CoLockObjectExternal(
                //    static_cast<IDriverService*>(driver_service_.get()), TRUE, FALSE));

                //// Use STRONG registration to keep it registered
                //winrt::check_hresult(RegisterActiveObject(
                //    static_cast<IDriverService*>(driver_service_.get()),
                //    clsid, ACTIVEOBJECT_STRONG, &register_cookie_));

                //// Sanity check: retrieve proxy to confirm registration
                //winrt::com_ptr < IUnknown > service;
                //winrt::check_hresult(GetActiveObject(
                //    clsid, nullptr, service.put()));

                //// Setup done - unlock the service object
                //driver_semaphore_.release();

                //MSG msg;
                //while (GetMessage(&msg, nullptr, 0, 0))
                //{
                //    TranslateMessage(&msg);
                //    DispatchMessage(&msg);
                //}

                //DriverCleanup();
                //winrt::uninit_apartment();
            }
            catch (const std::exception& e)
            {
                logMessage(std::format("Driver service setup failed with error: {}", e.what()));
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
        // TODO
    }

    void ServerProvider::Cleanup()
    {
#if USE_HOOKS
        logMessage("Disabling server driver hooks...");
        DisableHooks();
#endif
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
                pose.qRotation.w = pose_overrides_[openVRID].qRotation.w;
                pose.qRotation.x = pose_overrides_[openVRID].qRotation.x;
                pose.qRotation.y = pose_overrides_[openVRID].qRotation.y;
                pose.qRotation.z = pose_overrides_[openVRID].qRotation.z;
            }

            pose.vecPosition[0] = pose_overrides_[openVRID].vecPosition[0];
            pose.vecPosition[1] = pose_overrides_[openVRID].vecPosition[1];
            pose.vecPosition[2] = pose_overrides_[openVRID].vecPosition[2];

            pose.poseIsValid = pose_overrides_[openVRID].poseIsValid;
            pose.deviceIsConnected = pose_overrides_[openVRID].deviceIsConnected;
        }

        return true;
    }

    void ServerProvider::SetPoseOverride(uint32_t id, bool isEnabled)
    {
        if (isEnabled) pose_overrides_[id] = vr::DriverPose_t();
        else pose_overrides_.erase(id);
        if (id == 0) m_is_head_override_active = isEnabled;
    }

    void ServerProvider::UpdateDriverPose(uint32_t id, vr::DriverPose_t pose)
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
}

#if defined(_MSC_VER)
    #define EXPORT __declspec(dllexport)
#elif defined(__GNUC__)
    #define EXPORT __attribute__((visibility("default")))
#else
    #define EXPORT
    #pragma warning Unknown dynamic link import/export semantics.
#endif

extern "C" EXPORT void* HmdDriverFactory(const char* pInterfaceName, int* pReturnCode)
{
    static amethyst::driver::implementation::ServerProvider k2_server_provider;
    static amethyst::driver::implementation::DriverWatchdog k2_watchdog_driver;

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

    return nullptr;
}
