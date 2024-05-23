using Sqlmaster.Protobuf;

namespace checkmate.Models;
    
public record UserCreator(string Password, string DeviceName, UserRole Role, Guid? ReaderId);
