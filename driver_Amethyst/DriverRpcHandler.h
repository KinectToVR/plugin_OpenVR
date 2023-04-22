#pragma once

using namespace System::Collections::Generic;
using namespace Amethyst::Plugins::Contract;

public ref class DriverRpcHandler sealed : DriverServerRpc::RpcHandler
{
public:
    explicit DriverRpcHandler(DriverServerRpc::DriverRpcService^ parent) : RpcHandler(parent)
    {
    }

    IEnumerable<System::ValueTuple<TrackerType, bool>>^ SetTrackerStateList(
        IEnumerable<TrackerBase^>^ trackerList, bool wantReply) override;

    IEnumerable<System::ValueTuple<TrackerType, bool>>^ UpdateTrackerList(
        IEnumerable<TrackerBase^>^ trackerList, bool wantReply) override;
    
    bool RequestVrRestart(System::String^ message) override;

private:
    static System::ValueTuple<TrackerType, bool> SetTrackerState(TrackerBase^ tracker);
    static System::ValueTuple<TrackerType, bool> UpdateTrackerPose(TrackerBase^ tracker);
};
