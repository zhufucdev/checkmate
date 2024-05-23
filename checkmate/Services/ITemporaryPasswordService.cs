namespace checkmate.Services;

public interface ITemporaryPasswordService
{
    ICollection<ITemporaryPassword> TemporaryPasswords { get; }
    ITemporaryPassword AddTimedPassword(int lifeSpanSeconds = 45, int length = 8);
    ITemporaryPassword AddOneUsePassword(int length = 24);
    ITemporaryPassword? GetValidPassword(string password);

    public interface ITemporaryPassword
    {
        string Value { get; }
        bool IsValid();
        void Invalidate();
        Task ToBeInvalid();
        object? Tag { get; set; }
    }
    
    public interface ITimedTemporaryPassword : ITemporaryPassword
    {
        int LifeSpanSeconds { get; }
    }
}