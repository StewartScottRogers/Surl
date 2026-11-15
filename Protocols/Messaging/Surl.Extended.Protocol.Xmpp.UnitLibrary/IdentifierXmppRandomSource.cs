using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// An <see cref="IXmppRandomSource"/> over an <see cref="IIdentifierRandom"/>, so a seeded instance repeats
/// stream IDs, generated resources and anonymous names run after run for the same exchange order
/// (ADR-0124 decision 6). None of them is secret and none grants anything: an anonymous name only exists
/// where <c>--allow-anonymous</c> already allows the login. SASL nonces, challenges and tokens are not drawn
/// here: they come from the authentication policy's own secret sources, never from this type.
/// </summary>
/// <param name="identifiers">The identifier source to draw from.</param>
public sealed class IdentifierXmppRandomSource(IIdentifierRandom identifiers) : IXmppRandomSource
{
    // Streams on different connections draw at once.
    private readonly object gate = new();

    /// <inheritdoc/>
    public string NextHexDigits(int count)
    {
        var bytes = new byte[(count + 1) / 2];
        lock (gate)
        {
            identifiers.NextBytes(bytes);
        }

        return Convert.ToHexStringLower(bytes)[..count];
    }
}
