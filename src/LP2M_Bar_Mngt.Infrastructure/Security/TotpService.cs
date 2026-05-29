using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LP2M_Bar_Mngt.Infrastructure.Security;

public sealed class TotpService
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int Digits = 6;
    private const int PeriodSeconds = 30;

    public string GenerateSecret(int byteLength = 20)
    {
        return ToBase32(RandomNumberGenerator.GetBytes(byteLength));
    }

    public string BuildOtpAuthUri(string issuer, string username, string secret)
    {
        var label = Uri.EscapeDataString($"{issuer}:{username}");
        var escapedIssuer = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{label}?secret={secret}&issuer={escapedIssuer}&digits={Digits}&period={PeriodSeconds}";
    }

    public bool VerifyCode(string secret, string code, DateTimeOffset? now = null)
    {
        var normalizedCode = new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
        if (normalizedCode.Length != Digits || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;
        for (var offset = -1; offset <= 1; offset++)
        {
            var expected = ComputeCode(secret, timestamp.AddSeconds(offset * PeriodSeconds));
            if (FixedTimeEquals(expected, normalizedCode))
            {
                return true;
            }
        }

        return false;
    }

    public string GetCurrentCode(string secret, DateTimeOffset? now = null)
    {
        return ComputeCode(secret, now ?? DateTimeOffset.UtcNow);
    }

    private static string ComputeCode(string secret, DateTimeOffset timestamp)
    {
        var key = FromBase32(secret);
        var counter = timestamp.ToUnixTimeSeconds() / PeriodSeconds;
        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    }

    private static string ToBase32(byte[] data)
    {
        var result = new StringBuilder((data.Length + 4) / 5 * 8);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                result.Append(Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            result.Append(Alphabet[(buffer << (5 - bitsLeft)) & 31]);
        }

        return result.ToString();
    }

    private static byte[] FromBase32(string secret)
    {
        var cleaned = new string(secret
            .Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '=')
            .Select(char.ToUpperInvariant)
            .ToArray());

        var bytes = new List<byte>(cleaned.Length * 5 / 8);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in cleaned)
        {
            var value = Alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new InvalidOperationException("Cle de double authentification invalide.");
            }

            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bytes.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }

        return bytes.ToArray();
    }
}
