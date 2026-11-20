using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> of a Redis connection (ADR-0182): it follows RESP
/// arrays of bulk strings across reads and writes the password of <c>AUTH</c> and of
/// <c>HELLO ... AUTH user password</c> as a bulk string holding
/// <see cref="LineBytesRedactor.SecretMask"/>, so the length is not shown either. Inline commands
/// go through <see cref="RedisInlineCommandRedaction"/>. Every other byte is logged as received.
/// </summary>
internal sealed class RedisReceivedBytesRedactor : IReceivedBytesRedactor
{
    private const int MaxHeaderBytes = 24;
    private const int MaxCapturedBytes = 32;
    private const int MaxInlineBytes = LineBytesRedactor.DefaultMaxHeldBytes;
    private const byte LineFeed = 0x0A;
    private const byte CarriageReturn = 0x0D;

    private enum Stage
    {
        CommandStart,
        InlineLine,
        InlineDiscard,
        ArrayHeader,
        ElementHeader,
        ElementPayload,
        ElementTrailer,
    }

    private enum Command
    {
        Other,
        Auth,
        Hello,
    }

    private readonly List<byte> held = [];
    private readonly List<byte> captured = [];
    private Stage stage = Stage.CommandStart;
    private Command command;
    private int elementCount;
    private int elementIndex;
    private long payloadRemaining;
    private int trailerRemaining;
    private bool masking;
    private bool capturing;
    private bool previousIsAuth;
    private bool beforePreviousIsAuth;

    /// <inheritdoc/>
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (var next in received)
        {
            Step(next, output);
        }
    }

    /// <inheritdoc/>
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
    }

    /// <inheritdoc/>
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (stage == Stage.InlineLine)
        {
            RedisInlineCommandRedaction.Write(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(held), output);
        }
        else if (stage is Stage.ArrayHeader or Stage.ElementHeader)
        {
            output.Write(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(held));
        }

        held.Clear();
        stage = Stage.CommandStart;
    }

    private void Step(byte next, ArrayBufferWriter<byte> output)
    {
        switch (stage)
        {
            case Stage.CommandStart:
                StartCommand(next, output);
                break;
            case Stage.InlineLine:
                ContinueInline(next, output);
                break;
            case Stage.InlineDiscard:
                DiscardInline(next, output);
                break;
            case Stage.ArrayHeader:
                ContinueArrayHeader(next, output);
                break;
            case Stage.ElementHeader:
                ContinueElementHeader(next, output);
                break;
            case Stage.ElementPayload:
                ContinuePayload(next, output);
                break;
            default:
                ContinueTrailer(next, output);
                break;
        }
    }

    private void StartCommand(byte next, ArrayBufferWriter<byte> output)
    {
        held.Clear();
        if (next == (byte)'*')
        {
            held.Add(next);
            stage = Stage.ArrayHeader;
            return;
        }

        stage = Stage.InlineLine;
        ContinueInline(next, output);
    }

    private void ContinueInline(byte next, ArrayBufferWriter<byte> output)
    {
        if (next == LineFeed)
        {
            var hadCarriageReturn = held.Count > 0 && held[^1] == CarriageReturn;
            var line = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(held);
            RedisInlineCommandRedaction.Write(hadCarriageReturn ? line[..^1] : line, output);
            output.Write(hadCarriageReturn ? "\r\n"u8 : "\n"u8);
            held.Clear();
            stage = Stage.CommandStart;
            return;
        }

        held.Add(next);
        if (held.Count > MaxInlineBytes)
        {
            output.Write(LineBytesRedactor.SecretMask);
            held.Clear();
            stage = Stage.InlineDiscard;
        }
    }

    private void DiscardInline(byte next, ArrayBufferWriter<byte> output)
    {
        if (next == LineFeed)
        {
            output.Write("\r\n"u8);
            stage = Stage.CommandStart;
        }
    }

    private void ContinueArrayHeader(byte next, ArrayBufferWriter<byte> output)
    {
        held.Add(next);
        if (next != LineFeed)
        {
            GiveUpOnHeaderWhenTooLong(output);
            return;
        }

        if (TryReadHeaderNumber(out var count) && count > 0)
        {
            elementCount = count;
            elementIndex = 0;
            command = Command.Other;
            previousIsAuth = false;
            beforePreviousIsAuth = false;
            WriteHeldRaw(output);
            stage = Stage.ElementHeader;
            return;
        }

        WriteHeldRaw(output);
        stage = Stage.CommandStart;
    }

    private void ContinueElementHeader(byte next, ArrayBufferWriter<byte> output)
    {
        held.Add(next);
        if (held[0] != (byte)'$')
        {
            WriteHeldRaw(output);
            stage = Stage.CommandStart;
            return;
        }

        if (next != LineFeed)
        {
            GiveUpOnHeaderWhenTooLong(output);
            return;
        }

        BeginElement(output);
    }

    private void GiveUpOnHeaderWhenTooLong(ArrayBufferWriter<byte> output)
    {
        if (held.Count > MaxHeaderBytes)
        {
            WriteHeldRaw(output);
            stage = Stage.CommandStart;
        }
    }

    private void BeginElement(ArrayBufferWriter<byte> output)
    {
        captured.Clear();
        capturing = false;
        masking = false;
        if (!TryReadHeaderNumber(out var length) || length < 0)
        {
            WriteHeldRaw(output);
            FinishElement();
            return;
        }

        masking = IsSecretElement();
        capturing = length <= MaxCapturedBytes;
        if (masking)
        {
            output.Write("$4\r\n"u8);
            output.Write(LineBytesRedactor.SecretMask);
            output.Write("\r\n"u8);
        }
        else
        {
            WriteHeldRaw(output);
        }

        held.Clear();
        payloadRemaining = length;
        trailerRemaining = 2;
        stage = length == 0 ? Stage.ElementTrailer : Stage.ElementPayload;
    }

    private void ContinuePayload(byte next, ArrayBufferWriter<byte> output)
    {
        if (capturing)
        {
            captured.Add(next);
        }

        if (!masking)
        {
            output.Write([next]);
        }

        payloadRemaining--;
        if (payloadRemaining == 0)
        {
            stage = Stage.ElementTrailer;
        }
    }

    private void ContinueTrailer(byte next, ArrayBufferWriter<byte> output)
    {
        if (!masking)
        {
            output.Write([next]);
        }

        trailerRemaining--;
        if (trailerRemaining == 0)
        {
            FinishElement();
        }
    }

    private void FinishElement()
    {
        if (elementIndex == 0)
        {
            command = ClassifyCommand();
        }

        beforePreviousIsAuth = previousIsAuth;
        previousIsAuth = capturing && Ascii.EqualsIgnoreCase(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(captured), "AUTH"u8);
        elementIndex++;
        held.Clear();
        stage = elementIndex == elementCount ? Stage.CommandStart : Stage.ElementHeader;
    }

    private Command ClassifyCommand()
    {
        var name = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(captured);
        if (!capturing)
        {
            return Command.Other;
        }

        if (Ascii.EqualsIgnoreCase(name, "AUTH"u8))
        {
            return Command.Auth;
        }

        return Ascii.EqualsIgnoreCase(name, "HELLO"u8) ? Command.Hello : Command.Other;
    }

    private bool IsSecretElement() => command switch
    {
        Command.Auth => elementCount == 3 ? elementIndex == 2 : elementIndex >= 1,
        Command.Hello => beforePreviousIsAuth,
        _ => false,
    };

    // The number between the one-byte type marker and the CRLF, or false when it is not one.
    private bool TryReadHeaderNumber(out int number)
    {
        number = 0;
        var text = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(held);
        return text.Length >= 4
            && text[^2] == CarriageReturn
            && int.TryParse(text[1..^2], out number);
    }

    private void WriteHeldRaw(ArrayBufferWriter<byte> output)
    {
        output.Write(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(held));
        held.Clear();
    }
}
