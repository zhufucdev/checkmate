using System.Collections.ObjectModel;
using Grpc.Core;

namespace checkmate;

public class ContinuityStreamOwner<T>
{
    private readonly Dictionary<IServerStreamWriter<T>, SemaphoreSlim> _dict = new();
    public IReadOnlyCollection<IServerStreamWriter<T>> SuspendedWriters => _dict.Keys;

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
}