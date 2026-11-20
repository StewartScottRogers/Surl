using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> of a Kafka client's bytes: it writes the SASL PLAIN message
/// (RFC 4616: <c>authzid NUL authcid NUL passwd</c>) with the password replaced by
/// <see cref="LineBytesRedactor.SecretMask"/>, the authorization and authentication names kept, and every other
/// byte as received (ADR-0182, decisions 1 and 6).
/// </summary>
/// <remarks>
/// <para>
/// Each request is an INT32 size and that many bytes. A SaslHandshake request is held until it is whole to read
/// the mechanism it names: <c>PLAIN</c> (matched exactly, as <see cref="KafkaSaslHandshakeAnswerer"/> does) makes the
/// next login messages secret, another mechanism does not (SCRAM's proofs are logged, ADR-0182 decision 8). After
/// a v1 handshake each SaslAuthenticate request is held and its <c>auth_bytes</c> masked; after a v0 handshake the
/// next frame is a raw token, held and masked the same way. Every other request is passed through the moment its
/// API key is known, so a Produce's payload is never held and never touched.
/// </para>
/// <para>
/// A held frame is never more than <see cref="MaxHeldBytes"/>: a larger SASL request is written as
/// <see cref="LineBytesRedactor.SecretMask"/> and the rest of it dropped; a larger handshake is passed through.
/// A held frame cut off at <see cref="Flush"/> is written as <see cref="LineBytesRedactor.SecretMask"/> when it
/// was secret.
/// </para>
/// </remarks>
internal sealed class KafkaSaslPlainBytesRedactor : IReceivedBytesRedactor
{
    /// <summary>The most bytes held for one frame: 64 KiB (ADR-0182, decision 6).</summary>
    public const int MaxHeldBytes = LineBytesRedactor.DefaultMaxHeldBytes;

    private const int SizePrefixBytes = 4;
    private const int ApiKeyEnd = SizePrefixBytes + 4;
    private const int RequestHeaderVersionOne = 1;

    private readonly List<byte> held = [];
    private FrameKind kind = FrameKind.Undecided;
    private int frameLength = -1;
    private int passThroughBytes;
    private int dropBytes;
    private bool isPlainChosen;
    private bool isRawTokenNext;

    private enum FrameKind
    {
        Undecided,
        Handshake,
        Authenticate,
        RawToken,
    }

    /// <inheritdoc/>
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var index = 0;
        while (index < received.Length)
        {
            if (passThroughBytes > 0)
            {
                var passed = Math.Min(passThroughBytes, received.Length - index);
                output.Write(received.Slice(index, passed));
                index += passed;
                passThroughBytes -= passed;
            }
            else if (dropBytes > 0)
            {
                var dropped = Math.Min(dropBytes, received.Length - index);
                index += dropped;
                dropBytes -= dropped;
            }
            else
            {
                Hold(received[index++], output);
            }
        }
    }

    /// <inheritdoc/>
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // The password is only ever in the client's bytes, so what the broker sent changes nothing.
    }

    /// <inheritdoc/>
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (held.Count > 0 && kind is FrameKind.Authenticate or FrameKind.RawToken)
        {
            output.Write(LineBytesRedactor.SecretMask);
        }
        else
        {
            output.Write(CollectionsMarshal.AsSpan(held));
        }

        held.Clear();
        passThroughBytes = 0;
        dropBytes = 0;
        NextFrame();
    }

    private void Hold(byte next, ArrayBufferWriter<byte> output)
    {
        held.Add(next);
        if (held.Count == SizePrefixBytes)
        {
            MeasureFrame(output);
        }
        else if (held.Count == ApiKeyEnd && kind == FrameKind.Undecided)
        {
            DecideKind(output);
            if (kind != FrameKind.Undecided && held.Count == frameLength)
            {
                Complete(output);
            }
        }
        else if (held.Count == frameLength)
        {
            Complete(output);
        }
        else if (held.Count > MaxHeldBytes)
        {
            GiveUp(output);
        }
    }

    // A size of zero or less carries no body: the four bytes are passed and the next four are a prefix again.
    private void MeasureFrame(ArrayBufferWriter<byte> output)
    {
        var size = BinaryPrimitives.ReadInt32BigEndian(CollectionsMarshal.AsSpan(held));
        if (size <= 0)
        {
            PassHeld(output, 0);
            return;
        }

        frameLength = SizePrefixBytes + size;
        if (isRawTokenNext)
        {
            kind = FrameKind.RawToken;
        }
    }

    private void DecideKind(ArrayBufferWriter<byte> output)
    {
        var apiKey = BinaryPrimitives.ReadInt16BigEndian(CollectionsMarshal.AsSpan(held)[SizePrefixBytes..]);
        if (apiKey == (short)KafkaApiKey.SaslHandshake)
        {
            kind = FrameKind.Handshake;
        }
        else if (apiKey == (short)KafkaApiKey.SaslAuthenticate && isPlainChosen)
        {
            kind = FrameKind.Authenticate;
        }
        else
        {
            PassHeld(output, frameLength - held.Count);
        }
    }

    private void GiveUp(ArrayBufferWriter<byte> output)
    {
        if (kind == FrameKind.Handshake)
        {
            PassHeld(output, frameLength - held.Count);
            return;
        }

        output.Write(LineBytesRedactor.SecretMask);
        dropBytes = frameLength - held.Count;
        held.Clear();
        NextFrame();
    }

    private void PassHeld(ArrayBufferWriter<byte> output, int remaining)
    {
        output.Write(CollectionsMarshal.AsSpan(held));
        held.Clear();
        passThroughBytes = Math.Max(remaining, 0);
        NextFrame();
    }

    private void Complete(ArrayBufferWriter<byte> output)
    {
        var frame = CollectionsMarshal.AsSpan(held);
        if (kind == FrameKind.Handshake)
        {
            NoteHandshake(frame[SizePrefixBytes..]);
            output.Write(frame);
        }
        else if (kind == FrameKind.RawToken)
        {
            output.Write(frame[..SizePrefixBytes]);
            WritePlainMessage(frame[SizePrefixBytes..], output);
            isRawTokenNext = false;
        }
        else if (kind == FrameKind.Authenticate)
        {
            WriteAuthenticate(frame, output);
        }
        else
        {
            output.Write(frame);
        }

        held.Clear();
        NextFrame();
    }

    private void NextFrame()
    {
        kind = FrameKind.Undecided;
        frameLength = -1;
    }

    private void NoteHandshake(ReadOnlySpan<byte> body)
    {
        isPlainChosen = false;
        isRawTokenNext = false;
        try
        {
            var reader = new KafkaPrimitiveReader(body.ToArray());
            var header = KafkaRequestHeader.Read(reader, RequestHeaderVersionOne);
            isPlainChosen = reader.ReadString() == "PLAIN";
            isRawTokenNext = isPlainChosen && header.ApiVersion == 0;
        }
        catch (KafkaMalformedRequestException)
        {
            // A handshake the broker cannot read starts no login, so no later frame is secret.
        }
    }

    // v0 and v1 SaslAuthenticate: the request header v1, then auth_bytes as a BYTES.
    private static void WriteAuthenticate(ReadOnlySpan<byte> frame, ArrayBufferWriter<byte> output)
    {
        try
        {
            var reader = new KafkaPrimitiveReader(frame[SizePrefixBytes..].ToArray());
            KafkaRequestHeader.Read(reader, RequestHeaderVersionOne);
            var bytesStart = SizePrefixBytes + reader.Position + sizeof(int);
            var authBytes = reader.ReadBytes();
            output.Write(frame[..bytesStart]);
            WritePlainMessage(authBytes.Span, output);
            output.Write(frame[(bytesStart + authBytes.Length)..]);
        }
        catch (KafkaMalformedRequestException)
        {
            output.Write(frame[..SizePrefixBytes]);
            output.Write(LineBytesRedactor.SecretMask);
        }
    }

    // authzid NUL authcid NUL passwd: both names stay, the password goes; a message with fewer NULs is all secret.
    private static void WritePlainMessage(ReadOnlySpan<byte> message, ArrayBufferWriter<byte> output)
    {
        var first = message.IndexOf((byte)0);
        var second = first < 0 ? -1 : message[(first + 1)..].IndexOf((byte)0);
        if (second >= 0)
        {
            output.Write(message[..(first + 1 + second + 1)]);
        }

        output.Write(LineBytesRedactor.SecretMask);
    }
}
