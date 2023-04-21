#pragma once
#include "BodyTracker.h"
#include "DriverRpcHandler.h"

inline std::vector<BodyTracker> tracker_vector_;

using namespace System::Collections::Generic;
using namespace Amethyst::Plugins::Contract;

ref class DriverRpcHandler;
public ref class DriverService : DriverServerRpc::DriverRpcService
{
public:
    Object^ GetRpcHandler(DriverRpcService^ parent) override
    {
        return gcnew DriverRpcHandler(parent);
    }
};

ref struct server
{
    static DriverService^ service = gcnew DriverService();
};