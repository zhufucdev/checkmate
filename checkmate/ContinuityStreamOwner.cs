using Grpc.Core;

namespace checkmate;

public class ContinuityStreamOwner<T> : IDisposable
{
    private readonly Dictionary<IServerStreamWriter<T>, AsyncMutex> _dict = new();
    public IReadOnlyCollection<IServerStreamWriter<T>> SuspendedWriters => _dict.Keys;
    private readonly Timer? _heartbeatTimer;
    private readonly T? _heartbeatPackage;

    public ContinuityStreamOwner(T heartbeatPackage)
    {
        _heartbeatPackage = heartbeatPackage;
        _heartbeatTimer = new Timer(_heartbeat, null, 0, 30000);
    }

    public ContinuityStreamOwner()
    {
        _heartbeatTimer = null;
        _heartbeatPackage = default;
    }

    private void _heartbeat(object? _)
    {
        var __ = WriteAsync(_heartbeatPackage!);
    }

    public async Task Hold(IServerStreamWriter<T> writer)
    {
        using var mutex = new AsyncMutex();
        _dict.Add(writer, mutex);
        await mutex.Lock();
        await mutex.ToRelease();
        _dict.Remove(writer);
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

        _heartbeatTimer?.Dispose();
    }
}