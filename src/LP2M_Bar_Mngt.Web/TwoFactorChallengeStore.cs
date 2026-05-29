using System.Collections.Concurrent;
using LP2M_Bar_Mngt.Infrastructure.Security;

namespace LP2M_Bar_Mngt.Web;

internal sealed class TwoFactorChallengeStore
{
    private readonly ConcurrentDictionary<string, TwoFactorChallenge> _challenges = new();
    private readonly TimeSpan _lifetime = TimeSpan.FromMinutes(5);

    public TwoFactorChallenge Create(long userId, string username, string fullName, string role, string secret, bool rememberMe)
    {
        CleanupExpired();
        var challenge = new TwoFactorChallenge(
            Guid.NewGuid().ToString("N"),
            userId,
            username,
            fullName,
            role,
            secret,
            rememberMe,
            DateTimeOffset.UtcNow.Add(_lifetime),
            0);

        _challenges[challenge.Id] = challenge;
        return challenge;
    }

    public TwoFactorChallenge? Verify(string challengeId, string code, TotpService totpService)
    {
        if (string.IsNullOrWhiteSpace(challengeId) ||
            !_challenges.TryGetValue(challengeId, out var challenge) ||
            challenge.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _challenges.TryRemove(challengeId ?? string.Empty, out _);
            return null;
        }

        if (challenge.Attempts >= 5)
        {
            _challenges.TryRemove(challengeId, out _);
            return null;
        }

        if (!totpService.VerifyCode(challenge.Secret, code))
        {
            _challenges[challengeId] = challenge with { Attempts = challenge.Attempts + 1 };
            return null;
        }

        _challenges.TryRemove(challengeId, out _);
        return challenge;
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in _challenges)
        {
            if (item.Value.ExpiresAt <= now)
            {
                _challenges.TryRemove(item.Key, out _);
            }
        }
    }
}

internal sealed record TwoFactorChallenge(
    string Id,
    long UserId,
    string Username,
    string FullName,
    string Role,
    string Secret,
    bool RememberMe,
    DateTimeOffset ExpiresAt,
    int Attempts);
