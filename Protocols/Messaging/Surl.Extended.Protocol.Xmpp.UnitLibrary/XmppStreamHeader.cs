namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// What the server reads from a client's stream header (RFC 6120 section 4.7), its namespaces
/// already checked.
/// </summary>
/// <param name="To">The <c>to</c> attribute, or <see langword="null"/> when absent.</param>
/// <param name="Version">The <c>version</c> attribute, or <see langword="null"/> when absent.</param>
/// <param name="Language">The <c>xml:lang</c> attribute, or <see langword="null"/> when absent.</param>
/// <param name="IsEmptyElement">Whether the header was written <c>&lt;stream:stream/&gt;</c>, opening and closing the stream at once.</param>
internal sealed record XmppStreamHeader(string? To, string? Version, string? Language, bool IsEmptyElement);
