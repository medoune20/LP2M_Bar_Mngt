using System.Security.Cryptography;

namespace Infrastructure.Securite;

public static class PasswordHelper
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private const string Prefix = "PBKDF2";

    public static string Hasher(string motDePasse)
    {
        if (string.IsNullOrWhiteSpace(motDePasse))
        {
            throw new ArgumentException("Le mot de passe est obligatoire.", nameof(motDePasse));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            motDePasse,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool Verifier(string motDePasseSaisi, string motDePasseStocke)
    {
        if (string.IsNullOrEmpty(motDePasseSaisi) || string.IsNullOrEmpty(motDePasseStocke))
        {
            return false;
        }

        if (!motDePasseStocke.StartsWith($"{Prefix}$", StringComparison.Ordinal))
        {
            // Compatibilité avec les anciens comptes stockés en clair.
            return string.Equals(motDePasseSaisi, motDePasseStocke, StringComparison.Ordinal);
        }

        var parties = motDePasseStocke.Split('$');
        if (parties.Length != 4 || !int.TryParse(parties[1], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parties[2]);
            var keyStockee = Convert.FromBase64String(parties[3]);
            var keySaisie = Rfc2898DeriveBytes.Pbkdf2(
                motDePasseSaisi,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                keyStockee.Length);

            return CryptographicOperations.FixedTimeEquals(keySaisie, keyStockee);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool EstHash(string valeur)
    {
        return !string.IsNullOrWhiteSpace(valeur)
            && valeur.StartsWith($"{Prefix}$", StringComparison.Ordinal);
    }

    /// <summary>
    /// Longueur minimale exigée pour un mot de passe robuste.
    /// </summary>
    public const int LongueurMinimale = 8;

    /// <summary>
    /// Vérifie la complexité d'un mot de passe : au moins 8 caractères, avec une
    /// majuscule, une minuscule, un chiffre et un caractère spécial.
    /// Retourne <c>null</c> si le mot de passe est conforme, sinon un message d'erreur.
    /// </summary>
    public static string? ValiderComplexite(string? motDePasse)
    {
        motDePasse ??= string.Empty;

        if (motDePasse.Length < LongueurMinimale)
            return $"Le mot de passe doit contenir au moins {LongueurMinimale} caractères.";

        if (!motDePasse.Any(char.IsUpper))
            return "Le mot de passe doit contenir au moins une lettre majuscule.";

        if (!motDePasse.Any(char.IsLower))
            return "Le mot de passe doit contenir au moins une lettre minuscule.";

        if (!motDePasse.Any(char.IsDigit))
            return "Le mot de passe doit contenir au moins un chiffre.";

        if (motDePasse.All(char.IsLetterOrDigit))
            return "Le mot de passe doit contenir au moins un caractère spécial (ex. ! @ # $ % ...).";

        return null;
    }
}
