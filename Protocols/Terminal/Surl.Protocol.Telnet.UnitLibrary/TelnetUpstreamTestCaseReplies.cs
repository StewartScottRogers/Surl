namespace Surl.Protocol.Telnet;

/// <summary>
/// The seam that lets one upstream curl test case play the TELNET server half (ADR-0080 decision 2,
/// amended 2026-11-11 for test 1452). Handed to <see cref="TelnetProtocolServer"/>, it puts the
/// server in <b>echo-and-close mode</b>: no banner, the option negotiation as usual, every data
/// byte the client sends echoed verbatim with no line ending added, and the connection closed when
/// the client half-closes or, once it has sent something, goes quiet for <see cref="IdlePeriod"/>.
/// Every byte read from the client is recorded in arrival order in <see cref="ReceivedBytes"/>.
/// </summary>
/// <remarks>
/// Never composed by the shipped <c>surl</c>: <c>Surl.Conformance.UnitTests</c> composes it for
/// upstream's test cases. Without it the server answers as ADR-0025 says.
/// </remarks>
public sealed class TelnetUpstreamTestCaseReplies
{
    private readonly object gate = new();
    private readonly List<byte> received = [];

    /// <summary>
    /// Creates the seam.
    /// </summary>
    /// <param name="idlePeriod">How long the client may be quiet, after it has sent something, before the server closes.</param>
    public TelnetUpstreamTestCaseReplies(TimeSpan idlePeriod)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idlePeriod, TimeSpan.Zero);

        IdlePeriod = idlePeriod;
    }

    /// <summary>
    /// How long the client may be quiet, once it has sent something, before the server closes the connection.
    /// </summary>
    public TimeSpan IdlePeriod { get; }

    /// <summary>
    /// Every byte read from the client so far, in arrival order, negotiation included.
    /// </summary>
    public byte[] ReceivedBytes
    {
        get
        {
            lock (gate)
            {
                return [.. received];
            }
        }
    }

    internal bool HasReceivedAnything
    {
        get
        {
            lock (gate)
            {
                return received.Count > 0;
            }
        }
    }

    internal void Record(ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            received.AddRange(bytes);
        }
    }
}
