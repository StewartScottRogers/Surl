using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// Redacts the bytes of one SOCKS handshake and finds where it ends: it logs everything the client
/// sent as it arrived except the <c>PASSWD</c> of the RFC 1929 username/password subnegotiation
/// (<c>VER ULEN UNAME PLEN PASSWD</c>), which is written as <c>****</c> with its <c>PLEN</c> byte
/// written as 4, even for an empty password, so the log never reveals the password's length (ADR-0182
/// decision 1). It follows the handshake's own length fields to the last byte of the request - SOCKS4,
/// SOCKS4a, or the SOCKS5 greeting, its RFC 1929 or RFC 1961 messages and its request - so a client
/// that sends ahead of the server's replies is followed as one that waits is (ADR-0182, amendment 12).
/// Nothing the server sends is needed, so how the client's bytes fall between reads and replies changes
/// nothing. What follows the greeting is read from the client's next byte: <c>05</c> starts a request;
/// otherwise RFC 1961 messages follow when the listener offers GSS-API and so does the greeting, because
/// the server then selects it; otherwise an RFC 1929 login, when that byte is <c>01</c> or the greeting
/// offered the password method. From bytes it cannot follow on, everything is logged unchanged and the
/// request is never found. No byte is held back.
/// </summary>
internal sealed class SocksHandshakeBytesRedactor
{
    private const byte GssApiContextMessage = 1;
    private const byte GssApiProtectionMessage = 2;
    private const byte IPv4AddressType = 1;
    private const byte DomainNameAddressType = 3;
    private const byte IPv6AddressType = 4;
    private const int PortLength = 2;

    // CD (1), DSTPORT (2) and DSTIP (4).
    private readonly byte[] socks4Fields = new byte[7];
    private readonly bool listenerOffersGssApi;

    // What reads the next byte; null once the request's last byte has been read.
    private Action<byte>? next;
    private Action? whenCounted;
    private int countedBytesLeft;
    private int passwordBytesLeft;
    private int socks4FieldCount;
    private int gssApiTokenLength;
    private bool isPasswordLengthNext;
    private bool offersGssApi;
    private bool offersUserPassword;

    /// <summary>Creates a redactor positioned before the client's version byte.</summary>
    /// <param name="listenerOffersGssApi">Whether the server selects method <c>01</c> when a greeting offers it.</param>
    public SocksHandshakeBytesRedactor(bool listenerOffersGssApi)
    {
        this.listenerOffersGssApi = listenerOffersGssApi;
        next = ReadVersion;
    }

    /// <summary>Whether the last byte of the SOCKS request has been read, so the tunnel's bytes follow.</summary>
    public bool HasReadRequest => next is null;

    /// <summary>
    /// Takes the client's next bytes up to the end of the request and writes what may be logged.
    /// </summary>
    /// <param name="received">The bytes received.</param>
    /// <param name="output">Where the loggable bytes are appended.</param>
    /// <returns>How many of <paramref name="received"/> belong to the handshake; the rest are the tunnel's.</returns>
    public int Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var taken = 0;
        while (taken < received.Length && !HasReadRequest)
        {
            Take(received[taken++], output);
        }

        return taken;
    }

    private void Take(byte value, ArrayBufferWriter<byte> output)
    {
        if (passwordBytesLeft > 0)
        {
            passwordBytesLeft--;
        }
        else if (isPasswordLengthNext)
        {
            MaskPassword(value, output);
        }
        else
        {
            output.Write([value]);
            next!(value);
        }
    }

    private void MaskPassword(byte length, ArrayBufferWriter<byte> output)
    {
        // The PLEN byte is written as the mask's length, so the log never shows the password's (ADR-0182 decision 1).
        output.Write([(byte)LineBytesRedactor.SecretMask.Length]);
        output.Write(LineBytesRedactor.SecretMask);
        isPasswordLengthNext = false;
        passwordBytesLeft = length;
        next = ReadRequestVersion;
    }

    private void FollowCountedBytes(int count, Action whenFollowed)
    {
        countedBytesLeft = count;
        whenCounted = whenFollowed;
        next = ReadCountedByte;
        if (count == 0)
        {
            whenFollowed();
        }
    }

    private void ReadCountedByte(byte value)
    {
        if (--countedBytesLeft == 0)
        {
            whenCounted!();
        }
    }

    private void LeaveUnfollowed(byte value) => next = LeaveUnfollowed;

    private void ReadVersion(byte value) => next = value switch
    {
        SocksExchange.Socks4Version => ReadSocks4Field,
        SocksExchange.Socks5Version => ReadMethodCount,
        _ => LeaveUnfollowed,
    };

    private void ReadSocks4Field(byte value)
    {
        socks4Fields[socks4FieldCount++] = value;
        if (socks4FieldCount == socks4Fields.Length)
        {
            next = ReadSocks4UserId;
        }
    }

    private void ReadSocks4UserId(byte value)
    {
        if (value == 0)
        {
            next = Socks4RequestReader.IsSocks4aAddress(socks4Fields.AsSpan(3)) ? ReadSocks4HostName : null;
        }
    }

    private void ReadSocks4HostName(byte value)
    {
        if (value == 0)
        {
            next = null;
        }
    }

    private void ReadMethodCount(byte value)
    {
        countedBytesLeft = value;
        next = value == 0 ? ReadByteAfterGreeting : ReadMethod;
    }

    private void ReadMethod(byte value)
    {
        offersGssApi |= value == Socks5Reply.GssApiMethod;
        offersUserPassword |= value == Socks5Reply.UserPasswordMethod;
        if (--countedBytesLeft == 0)
        {
            next = ReadByteAfterGreeting;
        }
    }

    private void ReadByteAfterGreeting(byte value)
    {
        if (value == SocksExchange.Socks5Version)
        {
            next = ReadRequestCommand;
        }
        else if (listenerOffersGssApi && offersGssApi)
        {
            next = ReadGssApiType;
        }
        else
        {
            // A byte other than 01 is still read as a login's version when the server may be waiting for one.
            next = value == Socks5Reply.UserPasswordVersion || offersUserPassword ? ReadUserNameLength : LeaveUnfollowed;
        }
    }

    private void ReadUserNameLength(byte value) => FollowCountedBytes(value, () => isPasswordLengthNext = true);

    private void ReadGssApiVersion(byte value) => next = ReadGssApiType;

    private void ReadGssApiType(byte value) => next = value switch
    {
        GssApiContextMessage => ReadContextTokenLengthHigh,
        GssApiProtectionMessage => ReadProtectionTokenLengthHigh,
        _ => LeaveUnfollowed,
    };

    private void ReadContextTokenLengthHigh(byte value) => ReadTokenLengthHigh(value, ReadGssApiVersion);

    private void ReadProtectionTokenLengthHigh(byte value) => ReadTokenLengthHigh(value, ReadRequestVersion);

    private void ReadTokenLengthHigh(byte value, Action<byte> afterToken)
    {
        gssApiTokenLength = value << 8;
        next = low => FollowCountedBytes(gssApiTokenLength | low, () => next = afterToken);
    }

    private void ReadRequestVersion(byte value) =>
        next = value == SocksExchange.Socks5Version ? ReadRequestCommand : LeaveUnfollowed;

    private void ReadRequestCommand(byte value) => next = ReadRequestReserved;

    private void ReadRequestReserved(byte value) => next = ReadAddressType;

    private void ReadAddressType(byte value)
    {
        switch (value)
        {
            case IPv4AddressType:
                FollowRequestEnd(4 + PortLength);
                break;
            case IPv6AddressType:
                FollowRequestEnd(16 + PortLength);
                break;
            case DomainNameAddressType:
                next = ReadDomainNameLength;
                break;
            default:
                next = LeaveUnfollowed;
                break;
        }
    }

    private void ReadDomainNameLength(byte value) => FollowRequestEnd(value + PortLength);

    private void FollowRequestEnd(int count) => FollowCountedBytes(count, () => next = null);
}
