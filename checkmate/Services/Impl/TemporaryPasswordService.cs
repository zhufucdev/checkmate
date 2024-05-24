using System.Security.Cryptography;

namespace checkmate.Services.Impl;

public class TemporaryPasswordService : ITemporaryPasswordService
{
    public ICollection<ITemporaryPasswordService.ITemporaryPassword> TemporaryPasswords { get; } = [];

    private class TimedTemporaryPassword : ITemporaryPasswordService.ITimedTemporaryPassword
    {
        private const string OctalNumbers = "0123456789";

        public string Value { get; }
        public int LifeSpanSeconds { get; }

        private readonly TemporaryPasswordService _parent;
        private readonly AsyncMutex _mutex = new();
        private readonly Timer _timer;

        public TimedTemporaryPassword(TemporaryPasswordService parent, int lifespan = 45, int length = 8)
        {
            Value = RandomNumberGenerator.GetString(new ReadOnlySpan<char>(OctalNumbers.ToCharArray()), length);
            LifeSpanSeconds = lifespan;
            _parent = parent;
            _ = _mutex.Lock();
            _timer = new Timer(_ => Invalidate(), null, TimeSpan.FromSeconds(lifespan), Timeout.InfiniteTimeSpan);
        }

        private readonly DateTime _createTime = DateTime.Now;
        private bool _invalidated;

        public bool IsValid()
        {
            return !_invalidated && _createTime.Add(TimeSpan.FromSeconds(LifeSpanSeconds)) > DateTime.Now;
        }

        public void Invalidate()
        {
            _parent.TemporaryPasswords.Remove(this);
            _invalidated = true;
            _mutex.Release();
            _timer.Dispose();
        }

        public async Task ToBeInvalid()
        {
            await _mutex.ToRelease();
        }

        public object? Tag { get; set; }
    }

    public ITemporaryPasswordService.ITemporaryPassword AddTimedPassword(int lifeSpanSeconds, int length)
    {
        var pwd = new TimedTemporaryPassword(this, lifeSpanSeconds, length);
        TemporaryPasswords.Add(pwd);
        return pwd;
    }

    private class OneTimeTemporaryPassword
        : ITemporaryPasswordService.ITemporaryPassword
    {
        public string Value { get; }

        private readonly TemporaryPasswordService _parent;
        private bool _invalidated;
        private readonly AsyncMutex _mutex = new();

        public OneTimeTemporaryPassword(TemporaryPasswordService parent, int length = 24)
        {
            Value = RandomNumberGenerator.GetHexString(length);
            _parent = parent;
            _ = _mutex.Lock();
        }

        public bool IsValid()
        {
            return !_invalidated;
        }

        public void Invalidate()
        {
            _parent.TemporaryPasswords.Remove(this);
            _invalidated = true;
            _mutex.Release();
            _mutex.Dispose();
        }

        public async Task ToBeInvalid()
        {
            await _mutex.ToRelease();
        }

        public object? Tag { get; set; }
    }

    public ITemporaryPasswordService.ITemporaryPassword AddOneUsePassword(int length)
    {
        var pwd = new OneTimeTemporaryPassword(this, length);
        TemporaryPasswords.Add(pwd);
        return pwd;
    }

    public ITemporaryPasswordService.ITemporaryPassword? GetValidPassword(string password)
    {
        return TemporaryPasswords.FirstOrDefault(pwd => pwd!.IsValid() && pwd.Value == password, null);
    }
}