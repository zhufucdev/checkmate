using System.Security.Cryptography;
using System.Text;

namespace checkmate;

public class PasswordCrypto
{
    public static byte[] Hash(string password)
    {
        var bs = Encoding.UTF8.GetBytes(password);
        var hash = SHA1.HashData(bs);
        return Salted(hash);
    }

    private static byte[] Salted(byte[] data)
    {
        var salt = RandomNumberGenerator.GetBytes(4);
        var result = new byte[data.Length + salt.Length];
        data[..2].CopyTo(result, 0);
        salt.CopyTo(result, 2);
        data[2..].CopyTo(result, salt.Length+2);
        return data;
    }
}