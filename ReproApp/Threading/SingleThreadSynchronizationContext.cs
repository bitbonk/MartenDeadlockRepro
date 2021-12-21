using System.Collections.Concurrent;

namespace ReproApp.Threading;

/// <summary>
///     The implementation to provides <see cref="SynchronizationContext" /> based on a single named thread.
/// </summary>
/// <remarks>
///     A simple SynchronizationContext that encapsulates it's own dedicated task queue
///     and processing thread for servicing Post() calls.
///     Based upon <see href="http://blogs.msdn.com/b/pfxteam/archive/2012/01/20/10259049.aspx" />
///     but uses it's own thread rather than running on the thread that it's instantiated on.
/// </remarks>
internal sealed class SingleThreadSynchronizationContext : SynchronizationContext, IAsyncDisposable
{
    private readonly TimeSpan shutdownTimeout;

    private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    private readonly BlockingCollection<KeyValuePair<SendOrPostCallback, object?>> queue =
        new BlockingCollection<KeyValuePair<SendOrPostCallback, object?>>();

    private Thread? thread;
    private bool isDisposed;

    public SingleThreadSynchronizationContext(TimeSpan shutdownTimeout)
    {
        this.shutdownTimeout = shutdownTimeout;
    }

    /// <summary>
    ///     Starts a new thread with the specified name providing new <see cref="SynchronizationContext" />.
    /// </summary>
    /// <param name="name">The name of the thread to be started.</param>
    public void StartThread(string name)
    {
        // A background thread is used so that the simulation application can be terminated.
        this.thread = new Thread(this.ThreadFunction)
            { Name = name, IsBackground = true, Priority = ThreadPriority.AboveNormal };
        this.thread.Start();
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The thread and its synchronization context will be stopped and disposed.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        if (this.isDisposed)
        {
            // no thread to stop or cancellationToken already requested
            return new ValueTask(Task.CompletedTask);
        }

        this.isDisposed = true;
        if (this.thread == null || this.cancellationTokenSource.IsCancellationRequested)
        {
            // no thread to stop or cancellationToken already requested
            return new ValueTask(Task.CompletedTask);
        }

        this.cancellationTokenSource.Cancel();
        this.queue.CompleteAdding();
        return new ValueTask(
            Task.Run(
                () =>
                {
                    var isJoined = this.thread.Join(this.shutdownTimeout);
                    this.queue.Dispose();
                    if (!isJoined)
                    {
                        throw new InvalidOperationException(
                            $"Failed to stop thread {this.thread.Name} within {this.shutdownTimeout}.");
                    }
                }));
    }

    /// <inheritdoc />
    public override void Post(SendOrPostCallback d, object? state)
    {
        if (this.thread == null)
        {
            throw new InvalidOperationException("The thread must be started before using.");
        }

        if (this.isDisposed)
        {
            return;
        }

        this.queue.Add(new KeyValuePair<SendOrPostCallback, object?>(d, state));
    }

    /// <inheritdoc />
    public override void Send(SendOrPostCallback d, object? state)
    {
        throw new NotSupportedException("Synchronous calls are not allowed in this context.");
    }

    private void ThreadFunction(object? _)
    {
        SetSynchronizationContext(this);
        try
        {
            foreach (var workItem in this.queue.GetConsumingEnumerable(this.cancellationTokenSource.Token))
            {
                try
                {
                    var action = workItem.Key;
                    var state = workItem.Value;
                    action(state);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Unhandled exception in thread {this.thread?.Name}",
                        exception);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // just ignore
        }
    }
}