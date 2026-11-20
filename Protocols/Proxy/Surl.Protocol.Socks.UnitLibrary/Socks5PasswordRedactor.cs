using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> of one SOCKS exchange: it logs everything curl sent as it
/// arrived except the <c>PASSWD</c> of the RFC 1929 username/password subnegotiation
/// (<c>VER ULEN UNAME PLEN PASSWD</c>), which is written as <c>****</c> (ADR-0182). It follows the SOCKS5
/// greeting, waits to see the server select method <c>0x02</c>, then masks that one field, byte by byte,
/// so a split across reads at any byte changes nothing. SOCKS4 requests, other methods and every byte
/// after the subnegotiation (the tunnel included) are logged unchanged.
/// </summary>
internal sealed class Socks5PasswordRedactor : IReceivedBytesRedactor
{
    private const byte Socks5Version = 5;
    private const byte UserPasswordMethod = 2;

    private static readonly byte[] Mask = "****"u8.ToArray();

    private State state = State.Start;
    private int remaining;

    private enum State
    {
        Start,
        GreetingMethodCount,
        GreetingMethods,
        AwaitingMethodSelection,
        SubnegotiationVersion,
        UserNameLength,
        UserName,
        PasswordLength,
        Password,
        Unchanged,
    }

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (var value in received)
        {
            if (state == State.Password)
            {
                MaskPasswordByte();
                continue;
            }

            output.Write([value]);
            Advance(value, output);
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        if (state == State.AwaitingMethodSelection)
        {
            state = sent.Length >= 2 && sent[1] == UserPasswordMethod ? State.SubnegotiationVersion : State.Unchanged;
        }
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
    }

    private void MaskPasswordByte()
    {
        remaining--;
        if (remaining == 0)
        {
            state = State.Unchanged;
        }
    }

    private void Advance(byte value, ArrayBufferWriter<byte> output)
    {
        switch (state)
        {
            case State.Start:
                state = value == Socks5Version ? State.GreetingMethodCount : State.Unchanged;
                break;
            case State.GreetingMethodCount:
                BeginRun(value, State.GreetingMethods, State.AwaitingMethodSelection);
                break;
            case State.GreetingMethods:
                ContinueRun(State.GreetingMethods, State.AwaitingMethodSelection);
                break;
            case State.SubnegotiationVersion:
                state = State.UserNameLength;
                break;
            case State.UserNameLength:
                BeginRun(value, State.UserName, State.PasswordLength);
                break;
            case State.UserName:
                ContinueRun(State.UserName, State.PasswordLength);
                break;
            case State.PasswordLength:
                BeginPassword(value, output);
                break;
            default:
                break;
        }
    }

    private void BeginRun(byte length, State whileCounting, State whenEmpty)
    {
        remaining = length;
        state = length == 0 ? whenEmpty : whileCounting;
    }

    private void ContinueRun(State whileCounting, State whenDone)
    {
        remaining--;
        state = remaining == 0 ? whenDone : whileCounting;
    }

    private void BeginPassword(byte length, ArrayBufferWriter<byte> output)
    {
        remaining = length;
        if (length == 0)
        {
            state = State.Unchanged;

            return;
        }

        output.Write(Mask);
        state = State.Password;
    }
}