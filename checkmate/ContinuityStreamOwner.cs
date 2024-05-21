using Grpc.Core;

namespace checkmate;

public class ContinuityStreamOwner<T> : IDisposable
{
    private readonly Dictionary<IServerStreamWriter<T>, SemaphoreSlim> _dict = new();
    public IReadOnlyCollection<IServerStreamWriter<T>> SuspendedWriters => _dict.Keys;
    private readonly Timer _heartbeatTimer;
    private readonly T _heartbeatPackage;

    public ContinuityStreamOwner(T heartbeatPackage)
    {
        _heartbeatPackage = heartbeatPackage;
        _heartbeatTimer = new Timer(_heartbeat, null, 0, 30000);
    }

    private void _heartbeat(object? _)
    {
        WriteAsync(_heartbeatPackage);
    }

    public async Task Hold(IServerStreamWriter<T> writer)
    {
        using var mutex = new SemaphoreSlim(1, 1);
        _dict.Add(writer, mutex);
        await mutex.WaitAsync();
        await mutex.WaitAsync();
        _dict.Remove(writer);
        mutex.Release();
    }

    public void Release(IServerStreamWriter<T> writer)
    {
        _dict[writer].Release();
    }

    public async Task WriteAsync(T message)
    {
        await Task.WhenAll(SuspendedWriters.Select(writer =>
            writer.WriteAsync(message).ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    Release(writer);
                }
            }))
        );
    }

    public void Dispose()
    {
        foreach (var semaphore in _dict.Values)
        {
            semaphore.Release();
        }

        _heartbeatTimer.Dispose();
    }
}