using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using Amethyst.Plugins.Contract;
using IpcServer;
using MessageContract;
using MessagePack.Resolvers;
using MessagePack;
using Nerdbank.Streams;
using StreamJsonRpc;

namespace DriverServerRpc;

public abstract class DriverRpcService
{
    protected abstract object GetRpcHandler(DriverRpcService parent);

    public void SetupRunner(string serverName)
    {
        Logger.Info("Starting a detached task runner...");
        _ = Task.Run(async () => await SetupInternalAsync(serverName));
    }

    private async Task SetupInternalAsync(string serverName)
    {
        var clientId = 0;
        while (true)
        {
            Logger.Info("Waiting for client to make a connection...");

            var stream = new NamedPipeServerStream(serverName, PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            await stream.WaitForConnectionAsync();
            var nowait = RespondToRpcRequestsAsync(stream, ++clientId);
        }
    }

    private async Task RespondToRpcRequestsAsync(Stream stream, int clientId)
    {
        try
        {
            Logger.Info($"Connection request #{clientId} received. Spinning off a new Task...");

            Logger.Info("Setting up serialization resolvers...");
            var resolver = CompositeResolver.Create(
                NumericsResolver.Instance,
                StandardResolver.Instance
            );

            Logger.Info("Preparing the formatter...");
            var formatter = new MessagePackFormatter();
            formatter.SetMessagePackSerializerOptions(
                MessagePackSerializerOptions.Standard.WithResolver(resolver));

            Logger.Info("Setting up the message handler...");
            var serverHandler = new LengthHeaderMessageHandler(stream.UsePipe(), formatter);

            Logger.Info("Creating a new server instance...");
            var jsonRpc = new JsonRpc(serverHandler, GetRpcHandler(this))
            {
                TraceSource = new TraceSource("Server", SourceLevels.Verbose)
            };

            jsonRpc.TraceSource.Listeners.Add(new ConsoleTraceListener());

            Logger.Info("Opening the server listener for clients...");
            jsonRpc.StartListening();

            Logger.Info($"JSON-RPC listener attached to #{clientId}. Waiting for requests...");
            await jsonRpc.Completion;

            Logger.Info($"Connection #{clientId} terminated.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Exception: {ex.GetType().Name} in {ex.Source}: {ex.Message}\n{ex.StackTrace}");
            Debugger.Break();
        }
    }

    #region Logging helpers for the CLR host

    public void InitLogging()
    {
        // Initialize the logger
        Logger.Init(Helpers.GetAppDataLogFileDir("VRDriver",
            $"VRDriver_{DateTime.Now:yyyyMMdd-HHmmss.ffffff}.log"));

        // Log status information
        Logger.Info($"CLR Runtime version: {typeof(string).Assembly.ImageRuntimeVersion}");
        Logger.Info($"Framework build number: {Environment.Version} (.NET Core)");
        Logger.Info($"Running on {Environment.OSVersion}");

        Logger.Info($"Amethyst driver version: {Assembly.GetExecutingAssembly().GetName().Version}");
    }

    public void LogInfo(object message)
    {
        Logger.Info(message);
    }

    public void LogWarn(object message)
    {
        Logger.Warn(message);
    }

    public void LogError(object message)
    {
        Logger.Error(message);
    }

    public void LogFatal(object message)
    {
        Logger.Fatal(message);
    }

    #endregion
}

public abstract class RpcHandler : IRpcServer
{
    public RpcHandler(DriverRpcService parent)
    {
        Parent = parent;
    }

    private DriverRpcService Parent { get; }

    public abstract IEnumerable<(TrackerType Tracker, bool Success)>?
        SetTrackerStateList(IEnumerable<TrackerBase> trackerList, bool wantReply);

    public abstract IEnumerable<(TrackerType Tracker, bool Success)>?
        UpdateTrackerList(IEnumerable<TrackerBase> trackerList, bool wantReply);

    public abstract IEnumerable<(TrackerType Tracker, bool Success)>?
        RefreshTrackerPoseList(IEnumerable<TrackerBase> trackerList, bool wantReply);

    public abstract bool RequestVrRestart(string message);

    public DateTime PingDriverService()
    {
        return DateTime.Now;
    }
}