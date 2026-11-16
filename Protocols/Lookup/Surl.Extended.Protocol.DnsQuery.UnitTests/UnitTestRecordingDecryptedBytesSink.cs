using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>A decrypted-bytes sink that keeps what it was told as Latin-1 text, so a test can look for a path or a payload.</summary>
internal sealed class UnitTestRecordingDecryptedBytesSink : IDecryptedBytesSink
{
    private readonly List<string> received = [];

    public string[] Texts { get { lock (received) { return [.. received]; } } }

    public bool HasReceived(string text) => Texts.Any(entry => entry.Contains(text, StringComparison.Ordinal));

    public void Received(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes)
    {
        lock (received)
        {
            received.Add(Encoding.Latin1.GetString(bytes));
        }
    }

    public void Sent(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes)
    {
    }
}
