using System.Buffers;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// A protocol's decision, for <see cref="LineBytesRedactor"/>, on what one received line may
/// show in the log (ADR-0182, decision 6).
/// </summary>
public interface ILineRedactionRule
{
    /// <summary>
    /// Writes the line as it may be logged: unchanged, or with each secret replaced by
    /// <see cref="LineBytesRedactor.SecretMask"/>.
    /// </summary>
    /// <param name="line">One whole line, without its CR LF.</param>
    /// <param name="output">Where the loggable line is appended, without a CR LF.</param>
    void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output);

    /// <summary>
    /// Shows the rule what the server sent, so it can mark the client's next line a secret.
    /// </summary>
    /// <param name="sent">The bytes sent.</param>
    void ObserveSent(ReadOnlySpan<byte> sent);
}
