#pragma once
#include "DriverService.h"
#include <openvr_driver.h>

#include <map>
#include <semaphore>

#include <kj/debug.h>
#include <capnp/rpc-twoparty.h>

#define SUCCEEDED(hr) (((long)(hr)) >= 0)
#define FAILED(hr) (((long)(hr)) < 0)
#define CHECK_HR(hr) if (FAILED(hr)) { KJ_FAIL_REQUIRE(kj::str("HRESULT error: 0x", kj::hex(static_cast<uint32_t>(hr)))); }

namespace amethyst::driver::implementation
{
    class ServerProvider : public vr::IServerTrackedDeviceProvider, IRebuildCallback
    {
        std::shared_ptr<DriverService> driver_service_ = nullptr;
        std::map<ITrackerType, BodyTracker> tracker_vector_ = {};
        std::map<uint32_t, vr::DriverPose_t> pose_overrides_;
        std::counting_semaphore<1> driver_semaphore_{0};

    public:
        ServerProvider() = default;

        vr::EVRInitError Init(vr::IVRDriverContext *pDriverContext) override;

        void SetupService(uint32_t* port);

        void OnRebuildRequested() override;

        void DriverCleanup();

        void Cleanup() override;

        const char *const *GetInterfaceVersions() override;

        // It's running every frame
        void RunFrame() override;

        bool ShouldBlockStandbyMode() override;

        void EnterStandby() override;

        void LeaveStandby() override;

        bool HandleDevicePoseUpdated(uint32_t openVRID, vr::DriverPose_t &pose);

        void SetPoseOverride(uint32_t id, bool isEnabled);

        void UpdateDriverPose(uint32_t id, vr::DriverPose_t pose);
    };

    /*
      getVersion @0 () -> (apiVersion :UInt32);

      setTrackerState   @1 (tracker :TrackerBase) -> ();
      updateTracker     @2 (tracker :TrackerBase) -> ();

      requestVrRestart  @3 (message :Text) -> ();
      pingDriverService @4 () -> (ms :Int64);

      setDriverPose     @5 (id :UInt32, pose :DriverPose) -> ();
      enableOverride    @6 (id :UInt32, isEnabled :Bool) -> ();

      updateInputBoolean @7 (tracker :TrackerType, path :Text, value :Bool) -> ();
      updateInputScalar  @8 (tracker :TrackerType, path :Text, value :Float32) -> ();
    */

    class DriverImpl final : public IDriverService::Server
    {
        std::shared_ptr<DriverService> driver_service_ = nullptr;

    public:
        DriverImpl(std::shared_ptr<DriverService> driver_service) :
            driver_service_(driver_service) 
        {
        }

        inline std::shared_ptr<DriverService> driver_service() const
        {
            return driver_service_;
        }

        void driver_service(const std::shared_ptr<DriverService> &driver_service_)
        {
            this->driver_service_ = driver_service_;
        }

        kj::Promise<void> getVersion(GetVersionContext context) override
        {
            context.getResults().setApiVersion(2);
            return kj::READY_NOW;
        }

        kj::Promise<void> setTrackerState(SetTrackerStateContext context) override
        {
            CHECK_HR(driver_service()->SetTrackerState(context.getParams().getTracker()));
            return kj::READY_NOW;
        }

        kj::Promise<void> updateTracker(UpdateTrackerContext context) override
        {
            CHECK_HR(driver_service()->UpdateTracker(context.getParams().getTracker()));
            return kj::READY_NOW;
        }

        kj::Promise<void> requestVrRestart(RequestVrRestartContext context) override
        {
            CHECK_HR(driver_service()->RequestVrRestart(CapnpTextToWstring(context.getParams().getMessage())));
            return kj::READY_NOW;
        }

        kj::Promise<void> pingDriverService(PingDriverServiceContext context) override
        {
            context.getResults().setMs(std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now().time_since_epoch()).count());
            return kj::READY_NOW;
        }

        kj::Promise<void> setDriverPose(SetDriverPoseContext context) override
        {
            CHECK_HR(driver_service()->SetDriverPose(context.getParams().getId(), context.getParams().getPose()));
            return kj::READY_NOW;
        }

        kj::Promise<void> enableOverride(EnableOverrideContext context) override
        {
            CHECK_HR(driver_service()->EnableOverride(context.getParams().getId(), context.getParams().getIsEnabled()));
            return kj::READY_NOW;
        }

        kj::Promise<void> updateInputBoolean(UpdateInputBooleanContext context) override
        {
            CHECK_HR(driver_service()->UpdateInputBoolean(context.getParams().getTracker(), 
                CapnpTextToWstring(context.getParams().getPath()), context.getParams().getValue()));
            return kj::READY_NOW;
        }

        kj::Promise<void> updateInputScalar(UpdateInputScalarContext context) override
        {
            CHECK_HR(driver_service()->UpdateInputScalar(context.getParams().getTracker(),
                CapnpTextToWstring(context.getParams().getPath()), context.getParams().getValue()));
            return kj::READY_NOW;
        }

    private:
        inline std::wstring CapnpTextToWstring(capnp::Text::Reader txt)
        {
            const char* utf8 = txt.cStr();

            std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> conv;
            return conv.from_bytes(utf8, utf8 + txt.size());
        }
    };
}
