namespace checkmate;

public class AsyncMutex : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private Task? _initWait;

    public async Task ToRelease()
    {
        await _semaphore.WaitAsync();
    }

    public async Task Lock()
    {
        if (_initWait != null)
        {
            await _initWait;
        }
        
        _initWait = _semaphore.WaitAsync();
    }

    public void Release()
    {
        _semaphore.Release();
    }

    public void Dispose()
    {
        _initWait?.Dispose();
        _semaphore.Dispose();
    }
}