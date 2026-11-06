namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// Where the XMPP server draws its random values - stream IDs, generated resources and
/// anonymous names (ADR-0114 decision 0) - so a test can make them predictable.
/// </summary>
public interface IXmppRandomSource
{
    /// <summary>
    /// Draws <paramref name="count"/> random lower-case hexadecimal digits.
    /// </summary>
    /// <param name="count">How many digits; positive.</param>
    /// <returns>A string of <paramref name="count"/> characters from <c>0-9a-f</c>.</returns>
    string NextHexDigits(int count);
}
