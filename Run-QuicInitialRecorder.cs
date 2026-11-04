// Removes the packet protection from the QUIC version 1 client Initial packets upstream curl sent
// for --http3-only, so the TLS ClientHello it carries can be read before any QUIC server exists
// (BL-363). Record-CurlExchange.ps1 -QuicInitial records the datagrams and then runs it:
//
//   dotnet run --file Run-QuicInitialRecorder.cs -- --datagrams <datagrams.bin> --clienthello <clienthello.bin> --summary <clienthello.txt>
//   dotnet run --file Run-QuicInitialRecorder.cs -- --self-test
//
// datagrams.bin is every datagram curl sent, in arrival order, each preceded by its length as a
// two-byte big-endian number. Every long-header Initial packet in them (RFC 9000 section 17.2.2;
// coalesced packets are walked by their Length field) whose version is 1 has its header protection
// and AEAD_AES_128_GCM protection removed with the client Initial keys RFC 9001 section 5.2 derives
// from that packet's Destination Connection ID. The CRYPTO frames (RFC 9000 section 19.6) of the
// first Destination Connection ID's packets are put back together at their offsets - a
// retransmission repeats bytes it already gave - and the contiguous run from offset 0 is written to
// clienthello.bin: the TLS handshake message, type 1 (ClientHello) with its three-byte length. The
// summary names each packet, then the ClientHello's server name, ALPN list (RFC 7301) and
// quic_transport_parameters (extension 0x39, RFC 9000 section 18), one per line.
//
// --self-test decrypts the protected client Initial of RFC 9001 Appendix A.2 and compares the result
// with the appendix's unprotected CRYPTO frame and its PADDING. Exit codes: 0 done (or the self-test
// passed), 1 the self-test failed or no Initial packet could be decrypted, 2 a malformed command line.
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

return QuicInitialRecorder.Run(args);

/// <summary>Parses the command line and runs the decryption or the self-test.</summary>
internal static class QuicInitialRecorder
{
    public static int Run(string[] args)
    {
        if (args is ["--self-test"])
        {
            return QuicInitialSelfTest.Run();
        }

        var paths = ReadPaths(args);
        if (paths is null)
        {
            Console.Error.WriteLine("usage: Run-QuicInitialRecorder.cs --datagrams <file> --clienthello <file> --summary <file> | --self-test");
            return 2;
        }

        return Record(paths["--datagrams"], paths["--clienthello"], paths["--summary"]);
    }

    // The three paths by option name, or null when the command line is not exactly those three.
    private static Dictionary<string, string>? ReadPaths(string[] args)
    {
        string[] names = ["--datagrams", "--clienthello", "--summary"];
        var paths = new Dictionary<string, string>();
        for (var index = 0; index + 1 < args.Length; index += 2)
        {
            if (!names.Contains(args[index]) || !paths.TryAdd(args[index], args[index + 1]))
            {
                return null;
            }
        }

        return args.Length == 6 && paths.Count == 3 ? paths : null;
    }

    private static int Record(string datagramsPath, string clientHelloPath, string summaryPath)
    {
        var summary = new StringBuilder();
        var crypto = new CryptoStreamAssembler();
        byte[]? firstConnectionId = null;
        var datagramNumber = 0;
        foreach (var datagram in DatagramFile.Read(File.ReadAllBytes(datagramsPath)))
        {
            datagramNumber++;
            foreach (var packet in InitialPacketReader.Unprotect(datagram))
            {
                summary.AppendLine($"datagram {datagramNumber}: {packet.Describe()}");
                if (packet.Payload is null)
                {
                    continue;
                }

                firstConnectionId ??= packet.DestinationConnectionId;
                if (packet.DestinationConnectionId.AsSpan().SequenceEqual(firstConnectionId))
                {
                    QuicFrameReader.AddCryptoFrames(packet.Payload, crypto, summary);
                }
            }
        }

        var clientHello = crypto.ContiguousFromZero();
        File.WriteAllBytes(clientHelloPath, clientHello);
        summary.AppendLine($"clienthello.bin: {clientHello.Length} bytes of CRYPTO data from offset 0");
        ClientHelloReader.Describe(clientHello, summary);
        File.WriteAllText(summaryPath, summary.ToString(), new UTF8Encoding(false));
        Console.Out.Write(summary.ToString());
        return firstConnectionId is null ? 1 : 0;
    }
}

/// <summary>Splits datagrams.bin, each datagram behind a two-byte big-endian length.</summary>
internal static class DatagramFile
{
    public static IEnumerable<byte[]> Read(byte[] file)
    {
        var offset = 0;
        while (offset + 2 <= file.Length)
        {
            int length = BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan(offset));
            offset += 2;
            if (offset + length > file.Length)
            {
                throw new InvalidDataException($"datagrams.bin ends inside a {length}-byte datagram.");
            }

            yield return file.AsSpan(offset, length).ToArray();
            offset += length;
        }
    }
}

/// <summary>One long-header packet found in a datagram, decrypted when it is a version 1 Initial.</summary>
internal sealed record InitialPacket(uint Version, int Type, byte[] DestinationConnectionId, byte[] SourceConnectionId, int TokenLength, long PacketNumber, byte[]? Payload, string? Failure)
{
    public string Describe()
    {
        var head = $"long header version 0x{Version:x8} type {Type}, DCID {Convert.ToHexStringLower(DestinationConnectionId)}, SCID {Convert.ToHexStringLower(SourceConnectionId)}, token {TokenLength} bytes";
        return Payload is null ? $"{head}, not decrypted: {Failure}" : $"{head}, packet number {PacketNumber}, {Payload.Length}-byte payload";
    }
}

/// <summary>Reads the long-header packets of one datagram and removes an Initial's protection (RFC 9001 sections 5.3 and 5.4).</summary>
internal static class InitialPacketReader
{
    private const int TagLength = 16;
    private const int SampleLength = 16;

    public static List<InitialPacket> Unprotect(byte[] datagram)
    {
        var packets = new List<InitialPacket>();
        var offset = 0;
        while (offset < datagram.Length && (datagram[offset] & 0x80) != 0)
        {
            var packet = datagram.AsSpan(offset).ToArray();
            var cursor = 1;
            var version = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(cursor));
            cursor += 4;
            var destination = packet.AsSpan(cursor + 1, packet[cursor]).ToArray();
            cursor += 1 + destination.Length;
            var source = packet.AsSpan(cursor + 1, packet[cursor]).ToArray();
            cursor += 1 + source.Length;
            var type = (packet[0] >> 4) & 0x03;
            if (version != 1 || type != 0)
            {
                packets.Add(new InitialPacket(version, type, destination, source, 0, -1, null, "only version 1 Initial packets are decrypted"));
                break;
            }

            var tokenLength = (int)QuicVarint.Read(packet, ref cursor);
            cursor += tokenLength;
            var length = (int)QuicVarint.Read(packet, ref cursor);
            var packetNumberOffset = cursor;
            var end = packetNumberOffset + length;
            packets.Add(Decrypt(packet.AsSpan(0, end).ToArray(), version, type, destination, source, tokenLength, packetNumberOffset));
            offset += end;
        }

        return packets;
    }

    private static InitialPacket Decrypt(byte[] packet, uint version, int type, byte[] destination, byte[] source, int tokenLength, int packetNumberOffset)
    {
        var keys = InitialKeys.ForClient(destination);
        using var headerProtection = Aes.Create();
        headerProtection.Key = keys.HeaderProtection;
        var mask = headerProtection.EncryptEcb(packet.AsSpan(packetNumberOffset + 4, SampleLength), PaddingMode.None);
        packet[0] ^= (byte)(mask[0] & 0x0f);
        var packetNumberLength = (packet[0] & 0x03) + 1;
        long packetNumber = 0;
        for (var index = 0; index < packetNumberLength; index++)
        {
            packet[packetNumberOffset + index] ^= mask[1 + index];
            packetNumber = (packetNumber << 8) | packet[packetNumberOffset + index];
        }

        var nonce = (byte[])keys.Iv.Clone();
        for (var index = 0; index < 8; index++)
        {
            nonce[nonce.Length - 1 - index] ^= (byte)(packetNumber >> (8 * index));
        }

        var headerLength = packetNumberOffset + packetNumberLength;
        var ciphertext = packet.AsSpan(headerLength, packet.Length - headerLength - TagLength);
        var payload = new byte[ciphertext.Length];
        try
        {
            using var aead = new AesGcm(keys.Key, TagLength);
            aead.Decrypt(nonce, ciphertext, packet.AsSpan(packet.Length - TagLength), payload, packet.AsSpan(0, headerLength));
        }
        catch (AuthenticationTagMismatchException)
        {
            return new InitialPacket(version, type, destination, source, tokenLength, packetNumber, null, "the AEAD tag did not verify");
        }

        return new InitialPacket(version, type, destination, source, tokenLength, packetNumber, payload, null);
    }
}

/// <summary>The client Initial secrets of RFC 9001 section 5.2, for QUIC version 1.</summary>
internal sealed record InitialKeys(byte[] Key, byte[] Iv, byte[] HeaderProtection)
{
    private static readonly byte[] InitialSalt = Convert.FromHexString("38762cf7f55934b34d179ae6a4c80cadccbb7f0a");

    public static InitialKeys ForClient(byte[] destinationConnectionId)
    {
        var initialSecret = HKDF.Extract(HashAlgorithmName.SHA256, destinationConnectionId, InitialSalt);
        var clientSecret = ExpandLabel(initialSecret, "client in", 32);
        return new InitialKeys(ExpandLabel(clientSecret, "quic key", 16), ExpandLabel(clientSecret, "quic iv", 12), ExpandLabel(clientSecret, "quic hp", 16));
    }

    // HKDF-Expand-Label of RFC 8446 section 7.1, with an empty context.
    private static byte[] ExpandLabel(byte[] secret, string label, int length)
    {
        var fullLabel = Encoding.ASCII.GetBytes("tls13 " + label);
        var info = new byte[2 + 1 + fullLabel.Length + 1];
        BinaryPrimitives.WriteUInt16BigEndian(info, (ushort)length);
        info[2] = (byte)fullLabel.Length;
        fullLabel.CopyTo(info, 3);
        return HKDF.Expand(HashAlgorithmName.SHA256, secret, length, info);
    }
}

/// <summary>The variable-length integer of RFC 9000 section 16.</summary>
internal static class QuicVarint
{
    public static ulong Read(ReadOnlySpan<byte> bytes, ref int cursor)
    {
        var length = 1 << (bytes[cursor] >> 6);
        ulong value = (ulong)(bytes[cursor] & 0x3f);
        for (var index = 1; index < length; index++)
        {
            value = (value << 8) | bytes[cursor + index];
        }

        cursor += length;
        return value;
    }
}

/// <summary>Walks a decrypted Initial payload's frames (RFC 9000 section 19) and keeps the CRYPTO data.</summary>
internal static class QuicFrameReader
{
    public static void AddCryptoFrames(byte[] payload, CryptoStreamAssembler crypto, StringBuilder summary)
    {
        var cursor = 0;
        var padding = 0;
        while (cursor < payload.Length)
        {
            var frameType = QuicVarint.Read(payload, ref cursor);
            switch (frameType)
            {
                case 0x00:
                    padding++;
                    break;
                case 0x01:
                    summary.AppendLine("  PING");
                    break;
                case 0x06:
                    var offset = (long)QuicVarint.Read(payload, ref cursor);
                    var length = (int)QuicVarint.Read(payload, ref cursor);
                    crypto.Add(offset, payload.AsSpan(cursor, length));
                    summary.AppendLine($"  CRYPTO offset {offset} length {length}");
                    cursor += length;
                    break;
                default:
                    summary.AppendLine($"  frame type 0x{frameType:x} not read; the rest of the payload is skipped");
                    return;
            }
        }

        summary.AppendLine($"  PADDING {padding} bytes");
    }
}

/// <summary>Puts the CRYPTO stream back together at its offsets; a byte given twice keeps its first value.</summary>
internal sealed class CryptoStreamAssembler
{
    private readonly Dictionary<long, byte> bytes = [];

    public void Add(long offset, ReadOnlySpan<byte> data)
    {
        for (var index = 0; index < data.Length; index++)
        {
            bytes.TryAdd(offset + index, data[index]);
        }
    }

    public byte[] ContiguousFromZero()
    {
        var result = new List<byte>();
        while (bytes.TryGetValue(result.Count, out var value))
        {
            result.Add(value);
        }

        return [.. result];
    }
}

/// <summary>Names the ClientHello's server name, ALPN list and QUIC transport parameters (RFC 8446 section 4.1.2).</summary>
internal static class ClientHelloReader
{
    public static void Describe(byte[] message, StringBuilder summary)
    {
        if (message.Length < 4 || message[0] != 1 || message.Length < 4 + ((message[1] << 16) | (message[2] << 8) | message[3]))
        {
            summary.AppendLine("no complete ClientHello in the CRYPTO data");
            return;
        }

        var cursor = 4 + 2 + 32;
        cursor += 1 + message[cursor];
        cursor += 2 + BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(cursor));
        cursor += 1 + message[cursor];
        var extensionsEnd = cursor + 2 + BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(cursor));
        cursor += 2;
        var names = new List<string>();
        while (cursor + 4 <= extensionsEnd)
        {
            var type = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(cursor));
            var length = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(cursor + 2));
            var body = message.AsSpan(cursor + 4, length);
            names.Add($"0x{type:x4}");
            if (type == 0x0000)
            {
                summary.AppendLine($"server_name: {Encoding.ASCII.GetString(body[5..])}");
            }
            else if (type == 0x0010)
            {
                summary.AppendLine($"ALPN: {string.Join(", ", ReadAlpn(body))}");
            }
            else if (type == 0x0039)
            {
                DescribeTransportParameters(body.ToArray(), summary);
            }

            cursor += 4 + length;
        }

        summary.AppendLine($"extensions in order: {string.Join(" ", names)}");
    }

    private static List<string> ReadAlpn(ReadOnlySpan<byte> body)
    {
        var protocols = new List<string>();
        for (var cursor = 2; cursor < body.Length; cursor += 1 + body[cursor])
        {
            protocols.Add(Encoding.ASCII.GetString(body.Slice(cursor + 1, body[cursor])));
        }

        return protocols;
    }

    private static void DescribeTransportParameters(byte[] body, StringBuilder summary)
    {
        summary.AppendLine("quic_transport_parameters:");
        var cursor = 0;
        while (cursor < body.Length)
        {
            var id = QuicVarint.Read(body, ref cursor);
            var length = (int)QuicVarint.Read(body, ref cursor);
            var value = body.AsSpan(cursor, length).ToArray();
            cursor += length;
            summary.AppendLine($"  0x{id:x} {TransportParameterName(id)} = {Convert.ToHexStringLower(value)}{IntegerValue(id, value)}");
        }
    }

    // The integer-valued parameters of RFC 9000 section 18.2 and max_datagram_frame_size (RFC 9221).
    private static string IntegerValue(ulong id, byte[] value)
    {
        if (value.Length == 0 || id is 0x00 or 0x02 or 0x0c or 0x0d or 0x0f or 0x10 or 0x11 || id > 0x20)
        {
            return string.Empty;
        }

        var cursor = 0;
        return $" ({QuicVarint.Read(value, ref cursor)})";
    }

    private static string TransportParameterName(ulong id) => id switch
    {
        0x00 => "original_destination_connection_id",
        0x01 => "max_idle_timeout",
        0x02 => "stateless_reset_token",
        0x03 => "max_udp_payload_size",
        0x04 => "initial_max_data",
        0x05 => "initial_max_stream_data_bidi_local",
        0x06 => "initial_max_stream_data_bidi_remote",
        0x07 => "initial_max_stream_data_uni",
        0x08 => "initial_max_streams_bidi",
        0x09 => "initial_max_streams_uni",
        0x0a => "ack_delay_exponent",
        0x0b => "max_ack_delay",
        0x0c => "disable_active_migration",
        0x0d => "preferred_address",
        0x0e => "active_connection_id_limit",
        0x0f => "initial_source_connection_id",
        0x10 => "retry_source_connection_id",
        0x11 => "version_information",
        0x20 => "max_datagram_frame_size",
        0x2ab2 => "grease_quic_bit",
        _ => "unknown (a reserved or extension parameter)",
    };
}

/// <summary>RFC 9001 Appendix A.2: the protected client Initial, and the CRYPTO frame its payload must decrypt to.</summary>
internal static class QuicInitialSelfTest
{
    private const string ProtectedPacket =
        "c000000001088394c8f03e5157080000449e7b9aec34d1b1c98dd7689fb8ec11" +
        "d242b123dc9bd8bab936b47d92ec356c0bab7df5976d27cd449f63300099f399" +
        "1c260ec4c60d17b31f8429157bb35a1282a643a8d2262cad67500cadb8e7378c" +
        "8eb7539ec4d4905fed1bee1fc8aafba17c750e2c7ace01e6005f80fcb7df6212" +
        "30c83711b39343fa028cea7f7fb5ff89eac2308249a02252155e2347b63d58c5" +
        "457afd84d05dfffdb20392844ae812154682e9cf012f9021a6f0be17ddd0c208" +
        "4dce25ff9b06cde535d0f920a2db1bf362c23e596d11a4f5a6cf3948838a3aec" +
        "4e15daf8500a6ef69ec4e3feb6b1d98e610ac8b7ec3faf6ad760b7bad1db4ba3" +
        "485e8a94dc250ae3fdb41ed15fb6a8e5eba0fc3dd60bc8e30c5c4287e53805db" +
        "059ae0648db2f64264ed5e39be2e20d82df566da8dd5998ccabdae053060ae6c" +
        "7b4378e846d29f37ed7b4ea9ec5d82e7961b7f25a9323851f681d582363aa5f8" +
        "9937f5a67258bf63ad6f1a0b1d96dbd4faddfcefc5266ba6611722395c906556" +
        "be52afe3f565636ad1b17d508b73d8743eeb524be22b3dcbc2c7468d54119c74" +
        "68449a13d8e3b95811a198f3491de3e7fe942b330407abf82a4ed7c1b311663a" +
        "c69890f4157015853d91e923037c227a33cdd5ec281ca3f79c44546b9d90ca00" +
        "f064c99e3dd97911d39fe9c5d0b23a229a234cb36186c4819e8b9c5927726632" +
        "291d6a418211cc2962e20fe47feb3edf330f2c603a9d48c0fcb5699dbfe58964" +
        "25c5bac4aee82e57a85aaf4e2513e4f05796b07ba2ee47d80506f8d2c25e50fd" +
        "14de71e6c418559302f939b0e1abd576f279c4b2e0feb85c1f28ff18f58891ff" +
        "ef132eef2fa09346aee33c28eb130ff28f5b766953334113211996d20011a198" +
        "e3fc433f9f2541010ae17c1bf202580f6047472fb36857fe843b19f5984009dd" +
        "c324044e847a4f4a0ab34f719595de37252d6235365e9b84392b061085349d73" +
        "203a4a13e96f5432ec0fd4a1ee65accdd5e3904df54c1da510b0ff20dcc0c77f" +
        "cb2c0e0eb605cb0504db87632cf3d8b4dae6e705769d1de354270123cb11450e" +
        "fc60ac47683d7b8d0f811365565fd98c4c8eb936bcab8d069fc33bd801b03ade" +
        "a2e1fbc5aa463d08ca19896d2bf59a071b851e6c239052172f296bfb5e724047" +
        "90a2181014f3b94a4e97d117b438130368cc39dbb2d198065ae3986547926cd2" +
        "162f40a29f0c3c8745c0f50fba3852e566d44575c29d39a03f0cda721984b6f4" +
        "40591f355e12d439ff150aab7613499dbd49adabc8676eef023b15b65bfc5ca0" +
        "6948109f23f350db82123535eb8a7433bdabcb909271a6ecbcb58b936a88cd4e" +
        "8f2e6ff5800175f113253d8fa9ca8885c2f552e657dc603f252e1a8e308f76f0" +
        "be79e2fb8f5d5fbbe2e30ecadd220723c8c0aea8078cdfcb3868263ff8f09400" +
        "54da48781893a7e49ad5aff4af300cd804a6b6279ab3ff3afb64491c85194aab" +
        "760d58a606654f9f4400e8b38591356fbf6425aca26dc85244259ff2b19c41b9" +
        "f96f3ca9ec1dde434da7d2d392b905ddf3d1f9af93d1af5950bd493f5aa731b4" +
        "056df31bd267b6b90a079831aaf579be0a39013137aac6d404f518cfd4684064" +
        "7e78bfe706ca4cf5e9c5453e9f7cfd2b8b4c8d169a44e55c88d4a9a7f9474241" +
        "e221af44860018ab0856972e194cd934";

    private const string CryptoFrame =
        "060040f1010000ed0303ebf8fa56f12939b9584a3896472ec40bb863cfd3e868" +
        "04fe3a47f06a2b69484c00000413011302010000c000000010000e00000b6578" +
        "616d706c652e636f6dff01000100000a00080006001d00170018001000070005" +
        "04616c706e000500050100000000003300260024001d00209370b2c9caa47fba" +
        "baf4559fedba753de171fa71f50f1ce15d43e994ec74d748002b000302030400" +
        "0d0010000e0403050306030203080408050806002d00020101001c0002400100" +
        "3900320408ffffffffffffffff05048000ffff07048000ffff08011001048000" +
        "75300901100f088394c8f03e51570806048000ffff";

    public static int Run()
    {
        var packets = InitialPacketReader.Unprotect(Convert.FromHexString(ProtectedPacket));
        var expected = new byte[1162];
        Convert.FromHexString(CryptoFrame).CopyTo(expected, 0);
        var passed = packets is [{ Payload: { } payload, PacketNumber: 2 }] && payload.AsSpan().SequenceEqual(expected);
        Console.Out.WriteLine(passed
            ? "self-test passed: RFC 9001 Appendix A.2's protected client Initial decrypts to its CRYPTO frame and 1162-byte payload, packet number 2"
            : $"self-test FAILED: {string.Join("; ", packets.Select(packet => packet.Describe()))}");
        return passed ? 0 : 1;
    }
}
