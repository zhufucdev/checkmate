namespace checkmate.Models;

public record Session(int Id, byte[] Token, string Os, int UserId, DateTime LastAccess);