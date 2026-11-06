namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// A random source that is not random: each draw is the next number from 1, in hexadecimal,
/// zero-padded to the digits asked for, so a test can name every ID the server writes.
/// </summary>
internal sealed class CountingRandomSource : IXmppRandomSource
{
    private int draws;

    public string NextHexDigits(int count) => (++draws).ToString("x", System.Globalization.CultureInfo.InvariantCulture).PadLeft(count, '0');
}
