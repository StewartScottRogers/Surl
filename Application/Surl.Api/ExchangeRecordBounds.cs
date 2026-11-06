namespace Surl.Api;

/// <summary>
/// How much an <see cref="ExchangeRecordSubscriber"/> keeps: <see cref="SurlServerOptions.MaxRecordedExchanges"/>,
/// <see cref="SurlServerOptions.MaxRecordedBytesPerExchange"/> and <see cref="SurlServerOptions.MaxRecordedEvents"/>.
/// </summary>
/// <param name="MaxExchanges">The most exchanges kept; positive.</param>
/// <param name="MaxBytesPerExchange">The most bytes kept of one exchange; positive.</param>
/// <param name="MaxEvents">The most events kept; positive.</param>
internal sealed record ExchangeRecordBounds(int MaxExchanges, int MaxBytesPerExchange, int MaxEvents)
{
    /// <summary>The bounds <paramref name="options"/> sets.</summary>
    /// <param name="options">The server's options.</param>
    /// <returns>The bounds.</returns>
    public static ExchangeRecordBounds Of(SurlServerOptions options) =>
        new(options.MaxRecordedExchanges, options.MaxRecordedBytesPerExchange, options.MaxRecordedEvents);
}
