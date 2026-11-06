using System.Security.Cryptography;
using System.Text;

namespace Surl.Control;

/// <summary>
/// The bearer token every control request must carry (ADR-0124 decision 7): 32 to 1024 visible ASCII bytes, held
/// only as its SHA-256 hash and compared in fixed time, so neither the comparison nor a log reveals it.
/// </summary>
public sealed class ControlToken
{
    /// <summary>The fewest bytes a token may have.</summary>
    public const int MinLength = 32;

    /// <summary>The most bytes a token may have.</summary>
    public const int MaxLength = 1024;

    private const string BearerPrefix = "Bearer ";

    private readonly byte[] tokenHash;

    /// <summary>Holds <paramref name="token"/> as its hash.</summary>
    /// <param name="token">The token: 32 to 1024 bytes, each visible ASCII (<c>!</c> to <c>~</c>).</param>
    /// <exception cref="ArgumentException">The token is too short, too long or holds a byte outside visible ASCII.</exception>
    public ControlToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (token.Length is < MinLength or > MaxLength || !token.All(IsVisibleAscii))
        {
            throw new ArgumentException($"A control token is {MinLength} to {MaxLength} visible ASCII bytes.", nameof(token));
        }

        tokenHash = SHA256.HashData(Encoding.ASCII.GetBytes(token));
    }

    /// <summary>
    /// Whether <paramref name="fields"/> hold exactly one <c>Authorization</c> field, <c>Bearer</c> and this token.
    /// </summary>
    /// <param name="fields">A request's fields.</param>
    /// <returns><see langword="true"/> when the request carries this token.</returns>
    public bool IsPresentedIn(IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var authorizations = fields
            .Where(field => string.Equals(field.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
            .Select(field => field.Value)
            .ToArray();

        return authorizations.Length == 1
            && authorizations[0].StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            && CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(authorizations[0][BearerPrefix.Length..].Trim(' ', '\t'))),
                tokenHash);
    }

    private static bool IsVisibleAscii(char character) => character is >= '!' and <= '~';
}
