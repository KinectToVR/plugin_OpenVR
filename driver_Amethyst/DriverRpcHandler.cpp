#pragma once
#include "DriverRpcHandler.h"

#include <msclr/marshal_cppstd.h>
#include "DriverService.h"

using namespace System;
using namespace Collections::Generic;
using namespace Amethyst::Plugins::Contract;

IEnumerable<ValueTuple<TrackerType, bool>>^ DriverRpcHandler::SetTrackerStateList(
    IEnumerable<TrackerBase^>^ trackerList, bool wantReply)
{
    // Go over all trackers in the list and collect the results, execute
    auto result = Linq::Enumerable::ToList(Linq::Enumerable::Select(
        trackerList, gcnew Func<TrackerBase^, ValueTuple<TrackerType, bool>>(&DriverRpcHandler::SetTrackerState)));

    // Either return the result or save data
    return wantReply ? result : nullptr;
}

IEnumerable<ValueTuple<TrackerType, bool>>^ DriverRpcHandler::UpdateTrackerList(
    IEnumerable<TrackerBase^>^ trackerList, bool wantReply)
{
    // Go over all trackers in the list and collect the results, execute
    auto result = Linq::Enumerable::ToList(Linq::Enumerable::Select(
        trackerList, gcnew Func<TrackerBase^, ValueTuple<TrackerType, bool>>(&DriverRpcHandler::UpdateTrackerPose)));
    
    // Either return the result or save data
    return wantReply ? result : nullptr;
}

bool DriverRpcHandler::RequestVrRestart(String^ message)
{
    // Sanity check
    if (String::IsNullOrEmpty(message))
    {
        server::service->LogError(L"Couldn't request a reboot. The reason string is empty.");
        return false; // Compose the reply
    }

    // Perform the request
    server::service->LogInfo(L"Requesting OpenVR restart with reason: " + gcnew String(message));
    vr::VRServerDriverHost()->RequestRestart(
        msclr::interop::marshal_as<std::string>(message).c_str(),
        "vrstartup.exe", "", "");

    return true; // Compose the reply
}

ValueTuple<TrackerType, bool> DriverRpcHandler::SetTrackerState(TrackerBase^ tracker)
{
    if (tracker_vector_.size() > static_cast<int>(tracker->Role))
    {
        // Create a handle to the updated (native) tracker
        const auto pTracker = &tracker_vector_.at(static_cast<int>(tracker->Role));

        // Check the state and attempts spawning the tracker
        if (!pTracker->is_added() && !pTracker->spawn())
        {
            server::service->LogError(L"Couldn't spawn tracker with ID " +
                tracker->Role.ToString() + " due to an unknown native exception.");
            return ValueTuple<TrackerType, bool>(tracker->Role, false); // Failure
        }

        // Set the state of the native tracker
        pTracker->set_state(tracker->ConnectionState);

        server::service->LogError(
            L"Unmanaged (native) tracker with ID " + tracker->Role.ToString() +
            " state has been set to " + tracker->ConnectionState.ToString());

        // Call the VR update handler and compose the result
        tracker_vector_.at(static_cast<int>(tracker->Role)).update();
        return ValueTuple<TrackerType, bool>(tracker->Role, true);
    }

    server::service->LogError(L"Couldn't update tracker with ID " +
        tracker->Role.ToString() + ". The tracker index was out of bounds.");
    return ValueTuple<TrackerType, bool>(tracker->Role, false); // Failure
}

ValueTuple<TrackerType, bool> DriverRpcHandler::UpdateTrackerPose(TrackerBase^ tracker)
{
    if (tracker_vector_.size() > static_cast<int>(tracker->Role))
    {
        // Create a managed handle to the tracker object
        const auto pTracker = new gcroot<TrackerBase^>();
        *pTracker = tracker; // Set the handle root to the tracker

        // Update the pose of the passed tracker
        tracker_vector_.at(static_cast<int>(tracker->Role)).set_pose(pTracker);

        server::service->LogError(L"Updated tracker with ID " +
            tracker->Role.ToString() + " pose.");

        return ValueTuple<TrackerType, bool>(tracker->Role, true);
    }

    server::service->LogError(L"Couldn't update tracker with ID " +
        tracker->Role.ToString() + ". The tracker index was out of bounds.");
    return ValueTuple<TrackerType, bool>(tracker->Role, true); // Failure
}
