using Surl.Protocol.Abstractions;

namespace Surl.Output;

/// <summary>
/// The exchange log without <c>-v</c>: every call writes nothing.
/// </summary>
internal sealed class SilentExchangeLog : IExchangeLog
{
    /// <summary>
    /// The one instance; it holds no state.
    /// </summary>
    public static SilentExchangeLog Instance { get; } = new();

    private SilentExchangeLog()
    {
    }

    /// <inheritdoc/>
    public void BytesReceived(ReadOnlySpan<byte> bytes)
    {
    }

    /// <inheritdoc/>
    public void BytesSent(ReadOnlySpan<byte> bytes)
    {
    }

    /// <inheritdoc/>
    public void Note(string text) => ArgumentNullException.ThrowIfNull(text);
}
