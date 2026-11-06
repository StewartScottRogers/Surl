using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// Reads a call's Length-Prefixed-Messages from a request body that arrives in pieces: a message may be
/// split across any number of reads, and one read may hold several messages. Each message's length is judged
/// from its prefix before the message is read (ADR-0116 decision 8).
/// </summary>
public sealed class LengthPrefixedMessageReader
{
    private readonly int maximumMessageLength;
    private readonly long maximumCallLength;
    private byte[] pending = new byte[256];
    private int start;
    private int count;
    private long callLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="LengthPrefixedMessageReader" /> class.
    /// </summary>
    /// <param name="maximumMessageLength">The longest one message may be (<c>--max-message</c>).</param>
    /// <param name="maximumCallLength">The longest the call's messages may be together (<c>--max-filesize</c>).</param>
    public LengthPrefixedMessageReader(int maximumMessageLength, long maximumCallLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumMessageLength);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCallLength);
        this.maximumMessageLength = maximumMessageLength;
        this.maximumCallLength = maximumCallLength;
    }

    /// <summary>
    /// Adds the next bytes of the body.
    /// </summary>
    /// <param name="bytes">The bytes as they arrived.</param>
    public void Append(ReadOnlySpan<byte> bytes)
    {
        MakeRoom(bytes.Length);
        bytes.CopyTo(pending.AsSpan(start + count));
        count += bytes.Length;
    }

    /// <summary>
    /// Takes the next whole message, if all of it has arrived.
    /// </summary>
    /// <param name="message">The message, or <see langword="null" /> when it has not all arrived yet.</param>
    /// <returns><see langword="true" /> when a message was taken.</returns>
    /// <exception cref="GrpcCallException">
    /// <see cref="GrpcStatus.RequestMessageDidNotParse" /> for a Compressed-Flag other than 0 and 1;
    /// <see cref="GrpcStatus.MessageLargerThanTheLimit" /> for a Message-Length past the message limit;
    /// <see cref="GrpcStatus.CallLargerThanTheLimit" /> when the call's messages together pass the call limit.
    /// </exception>
    public bool TryRead([NotNullWhen(true)] out LengthPrefixedMessage? message)
    {
        message = null;
        if (count < LengthPrefixedMessage.PrefixLength)
        {
            return false;
        }

        var buffered = pending.AsSpan(start, count);
        var length = JudgePrefix(buffered);
        var total = LengthPrefixedMessage.PrefixLength + length;
        if (count < total)
        {
            return false;
        }

        message = new LengthPrefixedMessage(buffered[0] == 1, buffered[LengthPrefixedMessage.PrefixLength..total].ToArray());
        start += total;
        count -= total;
        callLength += length;
        return true;
    }

    /// <summary>
    /// Checks the body ended between messages, once its last byte has been appended and every whole message read.
    /// </summary>
    /// <exception cref="GrpcCallException"><see cref="GrpcStatus.RequestMessageDidNotParse" />: the body ended inside a message.</exception>
    public void Complete()
    {
        if (count > 0)
        {
            throw new GrpcCallException(GrpcStatus.RequestMessageDidNotParse);
        }
    }

    private int JudgePrefix(ReadOnlySpan<byte> buffered)
    {
        if (buffered[0] > 1)
        {
            throw new GrpcCallException(GrpcStatus.RequestMessageDidNotParse);
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(buffered[1..]);
        if (length > (uint)maximumMessageLength)
        {
            throw new GrpcCallException(GrpcStatus.MessageLargerThanTheLimit);
        }

        if (callLength + length > maximumCallLength)
        {
            throw new GrpcCallException(GrpcStatus.CallLargerThanTheLimit);
        }

        return (int)length;
    }

    private void MakeRoom(int extra)
    {
        var needed = count + extra;
        if (start + needed <= pending.Length)
        {
            return;
        }

        var target = needed <= pending.Length ? pending : new byte[Math.Max(needed, pending.Length * 2)];
        Array.Copy(pending, start, target, 0, count);
        pending = target;
        start = 0;
    }
}
