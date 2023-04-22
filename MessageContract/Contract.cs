using Amethyst.Plugins.Contract;
using MessagePack;
using System.Diagnostics.CodeAnalysis;

namespace MessageContract;

public interface IRpcServer
{
    public IEnumerable<(TrackerType Tracker, bool Success)>?
        SetTrackerStateList(IEnumerable<TrackerBase> trackerList, bool wantReply);

    public IEnumerable<(TrackerType Tracker, bool Success)>?
        UpdateTrackerList(IEnumerable<TrackerBase> trackerList, bool wantReply);
    
    public bool RequestVrRestart(string message);
    public DateTime PingDriverService();
}