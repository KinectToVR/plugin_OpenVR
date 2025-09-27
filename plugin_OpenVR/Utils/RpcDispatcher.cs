using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace plugin_OpenVR.Utils;

internal class RpcDispatcher : IDisposable
{
    private sealed class SingleThreadSynchronizationContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
        private int _threadId;

        public int ThreadId => _threadId;

        public void Run(CancellationToken token)
        {
            _threadId = Thread.CurrentThread.ManagedThreadId;
            try
            {
                while (true)
                {
                    var work = _queue.Take(token);
                    try
                    {
                        work.Callback(work.State);
                    }
                    catch
                    {
                        // Swallow to keep the loop alive; individual operations handle their own exceptions
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            _queue.Add((d, state));
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            if (Thread.CurrentThread.ManagedThreadId == _threadId)
            {
                d(state);
                return;
            }

            using var evt = new ManualResetEventSlim(false);
            Exception? ex = null;
            Post(s =>
            {
                try { d(s); }
                catch (Exception e) { ex = e; }
                finally { evt.Set(); }
            }, state);
            evt.Wait();
            if (ex is not null) throw new AggregateException(ex);
        }
    }

    private readonly object _gate = new();
    private SingleThreadSynchronizationContext? _context;
    private Thread? _thread;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource<bool>? _startedTcs;
    private bool _disposed;

    private void EnsureStarted()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RpcDispatcher));

        if (_thread is not null) return;

        lock (_gate)
        {
            if (_thread is not null) return;

            _cts = new CancellationTokenSource();
            _context = new SingleThreadSynchronizationContext();
            _startedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _thread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(_context);
                _startedTcs!.SetResult(true);
                _context.Run(_cts!.Token);
            }) { IsBackground = true, Name = "RpcDispatcherThread" };

            _thread.Start();
        }
    }

    private bool IsDispatcherThread => _thread is not null && Thread.CurrentThread.ManagedThreadId == _context?.ThreadId;

    public Task<T> InvokeAsync<T>(Func<Task<T>> func)
    {
        if (func is null) throw new ArgumentNullException(nameof(func));
        EnsureStarted();

        if (IsDispatcherThread)
        {
            // Already on the dispatcher thread
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                var tcsInline = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                tcsInline.SetException(ex);
                return tcsInline.Task;
            }
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _context!.Post(async _ =>
        {
            try
            {
                var result = await func().ConfigureAwait(true);
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);

        return tcs.Task;
    }

    public Task InvokeAsync(Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        EnsureStarted();

        if (IsDispatcherThread)
        {
            try
            {
                action();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                var tcsInline = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                tcsInline.SetException(ex);
                return tcsInline.Task;
            }
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _context!.Post(_ =>
        {
            try
            {
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);

        return tcs.Task;
    }

    public Task InvokeAsync(Func<Task> func)
    {
        if (func is null) throw new ArgumentNullException(nameof(func));
        EnsureStarted();

        if (IsDispatcherThread)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                var tcsInline = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                tcsInline.SetException(ex);
                return tcsInline.Task;
            }
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _context!.Post(async _ =>
        {
            try
            {
                await func().ConfigureAwait(true);
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);

        return tcs.Task;
    }

    public void Post(Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        EnsureStarted();

        _context!.Post(_ =>
        {
            try { action(); }
            catch
            {
                /* fire-and-forget: ignore exceptions to keep the dispatcher alive */
            }
        }, null);
    }

    public void Post(Func<Task> func)
    {
        if (func is null) throw new ArgumentNullException(nameof(func));
        EnsureStarted();

        _context!.Post(async _ =>
        {
            try { await func().ConfigureAwait(true); }
            catch
            {
                /* fire-and-forget: ignore exceptions to keep the dispatcher alive */
            }
        }, null);
    }

    public Task Resume()
    {
        EnsureStarted();
        return _startedTcs!.Task;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _cts?.Cancel();
            // Wake the loop if waiting
            _context?.Post(static _ => { }, null);
            _thread?.Join(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignored
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _thread = null;
            _context = null;
            _startedTcs = null;
        }
    }
}
