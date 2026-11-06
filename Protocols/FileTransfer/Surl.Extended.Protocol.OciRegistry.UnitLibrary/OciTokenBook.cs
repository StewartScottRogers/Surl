using System.Buffers.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The tokens one server has issued and not yet dropped, kept in memory (ADR-0115, decisions 4.3
/// and 7): each is 32 random bytes written base64url without padding, lives 300 seconds on the
/// clock, and at most <see cref="MaxLiveTokens"/> live at once. Not signed: a token never leaves
/// the server that checks it, so a lookup proves what a signature would.
/// </summary>
/// <param name="timeProvider">The clock tokens expire on.</param>
/// <param name="maxLiveTokens">How many live tokens it keeps; <see cref="MaxLiveTokens"/> unless a test lowers it.</param>
internal sealed class OciTokenBook(TimeProvider timeProvider, int maxLiveTokens = OciTokenBook.MaxLiveTokens)
{
    /// <summary>
    /// How many live tokens one server keeps; past it an issue is <c>429 TOOMANYREQUESTS</c>.
    /// </summary>
    public const int MaxLiveTokens = 10000;

    /// <summary>
    /// How long a token is accepted after it is issued, sent as <c>expires_in</c>.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(300);

    private const int TokenBytes = 32;

    private readonly Dictionary<string, OciToken> tokens = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>
    /// Issues a token to <paramref name="accountName"/> granting <paramref name="grants"/>,
    /// dropping expired tokens first when the book is full.
    /// </summary>
    /// <param name="accountName">The account, or <see langword="null"/> for an anonymous token.</param>
    /// <param name="grants">What it grants, by repository name.</param>
    /// <param name="random">Where the token's bytes come from.</param>
    /// <returns>The token's text, or <see langword="null"/> while the bound's number of tokens are live.</returns>
    public string? Issue(string? accountName, IReadOnlyDictionary<string, OciActions> grants, IIdentifierRandom random)
    {
        Span<byte> bytes = stackalloc byte[TokenBytes];
        random.NextBytes(bytes);
        var text = Base64Url.EncodeToString(bytes);
        var now = timeProvider.GetUtcNow();

        lock (gate)
        {
            if (tokens.Count >= maxLiveTokens)
            {
                foreach (var expired in tokens.Where(pair => pair.Value.ExpiresAt <= now).Select(pair => pair.Key).ToList())
                {
                    tokens.Remove(expired);
                }
            }

            if (tokens.Count >= maxLiveTokens)
            {
                return null;
            }

            tokens[text] = new OciToken(accountName, grants, now + Lifetime);
        }

        return text;
    }

    /// <summary>
    /// The live token whose text is <paramref name="text"/>; an expired one is dropped.
    /// </summary>
    /// <param name="text">The token as the client sent it.</param>
    /// <returns>The token, or <see langword="null"/> when it is unknown or expired.</returns>
    public OciToken? Find(string text)
    {
        lock (gate)
        {
            if (!tokens.TryGetValue(text, out var token))
            {
                return null;
            }

            if (token.ExpiresAt > timeProvider.GetUtcNow())
            {
                return token;
            }

            tokens.Remove(text);

            return null;
        }
    }
}
