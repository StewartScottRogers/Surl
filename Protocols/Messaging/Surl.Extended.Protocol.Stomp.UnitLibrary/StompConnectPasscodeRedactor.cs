using System.Buffers;
using System.Runtime.InteropServices;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> of the STOMP client's bytes: in the CONNECT or STOMP frame that
/// opens the connection it writes the value of the <c>passcode</c> header as
/// <see cref="LineBytesRedactor.SecretMask"/> and logs every other byte, <c>login</c> included, as received
/// (ADR-0182, decisions 1 and 6).
/// </summary>
/// <remarks>
/// STOMP lines end in LF or CR LF, so this is not a <see cref="LineBytesRedactor"/>. Only the first frame can
/// carry the passcode (any other first frame, or a second CONNECT, is answered ERROR), so once that frame's
/// headers end, or the first frame is not CONNECT or STOMP, every later byte is passed through at once. A header
/// line is held back until its LF, so a passcode split across reads is still masked. A line past
/// <see cref="MaxHeldBytes"/> is written as <see cref="LineBytesRedactor.SecretMask"/> and the rest of it is dropped
/// up to its LF, which is written as it arrived. The header name is matched exactly, as the server's reader does
/// (CONNECT headers are not unescaped).
/// </remarks>
internal sealed class StompConnectPasscodeRedactor : IReceivedBytesRedactor
{
    /// <summary>The most bytes held back while waiting for an LF: 64 KiB, as <see cref="LineBytesRedactor"/>.</summary>
    public const int MaxHeldBytes = LineBytesRedactor.DefaultMaxHeldBytes;

    private const byte LineFeed = 0x0A;
    private const byte CarriageReturn = 0x0D;

    private static ReadOnlySpan<byte> PasscodeName => "passcode"u8;

    private readonly List<byte> held = [];
    private Stage stage = Stage.Command;
    private bool discardingToLineFeed;

    private enum Stage
    {
        Command,
        ConnectHeaders,
        PassedThrough,
    }

    /// <inheritdoc/>
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        for (var index = 0; index < received.Length; index++)
        {
            if (stage == Stage.PassedThrough)
            {
                output.Write(received[index..]);
                return;
            }

            Take(received[index], output);
        }
    }

    /// <inheritdoc/>
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // The passcode is only ever in the client's CONNECT, so what the server sent changes nothing.
    }

    /// <inheritdoc/>
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        WriteContent(CollectionsMarshal.AsSpan(held), output);
        held.Clear();
        discardingToLineFeed = false;
    }

    private void Take(byte next, ArrayBufferWriter<byte> output)
    {
        if (discardingToLineFeed)
        {
            discardingToLineFeed = next != LineFeed;
            if (!discardingToLineFeed)
            {
                output.Write([LineFeed]);
            }

            return;
        }

        held.Add(next);
        if (next == LineFeed)
        {
            FinishLine(output);
        }
        else if (held.Count > MaxHeldBytes)
        {
            output.Write(LineBytesRedactor.SecretMask);
            held.Clear();
            discardingToLineFeed = true;
        }
    }

    private void FinishLine(ArrayBufferWriter<byte> output)
    {
        var whole = CollectionsMarshal.AsSpan(held);
        var terminator = held.Count > 1 && whole[^2] == CarriageReturn ? 2 : 1;
        var content = whole[..^terminator];
        WriteContent(content, output);
        output.Write(whole[content.Length..]);
        NextStage(content);
        held.Clear();
    }

    private void NextStage(ReadOnlySpan<byte> content)
    {
        if (stage == Stage.Command && content.Length > 0)
        {
            stage = content.SequenceEqual("CONNECT"u8) || content.SequenceEqual("STOMP"u8)
                ? Stage.ConnectHeaders
                : Stage.PassedThrough;
        }
        else if (stage == Stage.ConnectHeaders && content.Length == 0)
        {
            stage = Stage.PassedThrough;
        }
    }

    // A header line of the CONNECT frame is masked when it is the passcode; every other line is as received.
    private void WriteContent(ReadOnlySpan<byte> content, ArrayBufferWriter<byte> output)
    {
        var colon = content.IndexOf((byte)':');
        if (stage == Stage.ConnectHeaders && colon >= 0 && content[..colon].SequenceEqual(PasscodeName))
        {
            output.Write(content[..(colon + 1)]);
            output.Write(LineBytesRedactor.SecretMask);
            return;
        }

        output.Write(content);
    }
}
