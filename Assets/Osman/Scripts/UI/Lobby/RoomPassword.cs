using System.Security.Cryptography;
using System.Text;

// Oda şifresi düz gitmez: oda adıyla tuzlanmış SHA-256 özeti.
public static class RoomPassword
{
    public static string Hash(string roomName, string password)
    {
        string p = password == null ? "" : password.Trim();
        if (p.Length == 0)
            return "";
        byte[] h;
        using (SHA256 sha = SHA256.Create())
            h = sha.ComputeHash(Encoding.UTF8.GetBytes(roomName + "\n" + p));
        var sb = new StringBuilder(64);
        foreach (byte b in h)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public static bool Matches(string roomName, string entered, string hash) =>
        string.Equals(Hash(roomName, entered), hash ?? "", System.StringComparison.Ordinal);
}
