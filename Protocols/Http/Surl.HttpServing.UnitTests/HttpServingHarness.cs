using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.HttpServing;

/// <summary>
/// What the tests of the shared HTTP/1.1 serving library share: a clock that stands still, an
/// exchange context with chosen limits, and ASCII and Latin-1 conversions.
/// </summary>
internal static class HttpServingHarness
{
    /// <summary>
    /// The clock every response's <c>Date</c> is taken from: 2026-11-04 12:00:00 UTC, which
    /// IMF-fixdate writes as <see cref="NowHttpDate"/>.
    /// </summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    public const string NowHttpDate = "Wed, 04 Nov 2026 12:00:00 GMT";

    public static ExchangeContext Context(IExchangeLog log, TimeProvider timeProvider, CancellationToken cancellationToken, ExchangeLimits? limits = null) => new(
        1,
        new ListenUrl("http", "127.0.0.1", 18080).WithBoundPort(18080),
        new IPEndPoint(IPAddress.Loopback, 18080),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        timeProvider,
        cancellationToken)
    {
        Limits = limits ?? ExchangeLimits.Default,
    };

    public static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    public static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
