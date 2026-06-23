using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Securite;

/// <summary>Génération et empreinte des clés d'API.</summary>
public static class CleApiHelper
{
    public static (string cleComplete, string prefixe, string hash) Generer()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        var jeton = Convert.ToBase64String(bytes)
            .Replace("+", "").Replace("/", "").Replace("=", "");
        var cle = "lp2m_" + jeton;
        var prefixe = cle.Length > 12 ? cle[..12] : cle;
        return (cle, prefixe, Hash(cle));
    }

    public static string Hash(string? cle)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(cle ?? string.Empty));
        return Convert.ToHexString(bytes);
    }
}
