namespace Surl.Protocol.Abstractions;

/// <summary>
/// Which <see cref="IExchangeLog"/> member an <see cref="ExchangeLogEntry"/> records.
/// </summary>
public enum ExchangeLogEntryKind
{
    /// <summary>
    /// A call to <see cref="IExchangeLog.BytesReceived"/>.
    /// </summary>
    BytesReceived,

    /// <summary>
    /// A call to <see cref="IExchangeLog.BytesSent"/>.
    /// </summary>
    BytesSent,

    /// <summary>
    /// A call to <see cref="IExchangeLog.Note"/>.
    /// </summary>
    Note,
}
