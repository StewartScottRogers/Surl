namespace Surl.Protocol.Ssh;

/// <summary>
/// How one direction's binary packets are protected (RFC 4253 section 6): none before the
/// first <c>NEWKEYS</c>, then the cipher and MAC agreed for that direction, keyed from the key
/// exchange (ADR-0051, decision 2). Each instance holds its direction's running state - the
/// CTR counter or the GCM invocation counter - so it protects one direction only.
/// </summary>
/// <remarks>
/// A packet on the wire is read in two steps: first <see cref="HeadLength"/> bytes, from which
/// <see cref="OpenHead"/> gives the <c>packet_length</c>, then the rest of the packet and its
/// <see cref="TagLength"/>-byte MAC or tag, which <see cref="OpenBody"/> checks and decrypts.
/// </remarks>
internal abstract class SshPacketProtection
{
    /// <summary>
    /// The protection of packets before the first <c>NEWKEYS</c>: none, with 8-byte blocks.
    /// </summary>
    public static SshPacketProtection None { get; } = new SshNoPacketProtection();

    /// <summary>
    /// The block size packets are padded to: 8 with no cipher, the cipher's block size with one.
    /// </summary>
    public abstract int BlockSize { get; }

    /// <summary>
    /// Whether <c>packet_length</c> is encrypted, as it is under a cipher and an encrypt-and-MAC
    /// MAC and, with its own key, under <c>chacha20-poly1305@openssh.com</c>, rather than sent in the clear.
    /// </summary>
    public abstract bool EncryptsLength { get; }

    /// <summary>
    /// Whether the 4-byte <c>packet_length</c> counts towards the block alignment: it does
    /// unless it is kept apart from the rest of the packet - in the clear beside an encrypt-then-MAC
    /// MAC or AES-GCM, or under its own key with <c>chacha20-poly1305@openssh.com</c> - where
    /// only the rest of the packet is aligned (OpenSSH <c>PROTOCOL</c> sections 1.5 and 1.6, <c>PROTOCOL.chacha20poly1305</c>).
    /// </summary>
    public abstract bool AlignsLength { get; }

    /// <summary>
    /// The length of the MAC or AEAD tag after each packet; 0 with none.
    /// </summary>
    public abstract int TagLength { get; }

    /// <summary>
    /// How many bytes are read before <c>packet_length</c> is known: by default a whole block when it is
    /// encrypted with the rest of the packet, else its own 4 bytes.
    /// </summary>
    public virtual int HeadLength => EncryptsLength ? BlockSize : sizeof(uint);

    /// <summary>
    /// The protection agreed for one direction, keyed from <paramref name="keys"/>.
    /// </summary>
    /// <param name="cipher">The cipher agreed for the direction.</param>
    /// <param name="mac">The MAC agreed for the direction; <see langword="null"/> beside an AEAD cipher.</param>
    /// <param name="keys">The key exchange's key derivation.</param>
    /// <param name="clientToServer">
    /// Whether the direction is client to server, keyed with the letters <c>A</c>, <c>C</c> and
    /// <c>E</c>, rather than server to client, keyed with <c>B</c>, <c>D</c> and <c>F</c> (RFC 4253, section 7.2).
    /// </param>
    /// <returns>The protection, or <see langword="null"/> for a cipher or MAC not built.</returns>
    public static SshPacketProtection? Create(string cipher, string? mac, SshKeyDerivation keys, bool clientToServer)
    {
        var (ivLetter, keyLetter, macLetter) = clientToServer ? ('A', 'C', 'E') : ('B', 'D', 'F');
        if (cipher == SshChaCha20Poly1305Protection.Name)
        {
            return new SshChaCha20Poly1305Protection(keys.DeriveKey(keyLetter, SshChaCha20Poly1305Protection.KeyMaterialLength));
        }

        if (SshAesGcmProtection.KeyLengthFor(cipher) is { } gcmKeyLength)
        {
            return new SshAesGcmProtection(keys.DeriveKey(keyLetter, gcmKeyLength), keys.DeriveKey(ivLetter, SshAesGcmProtection.NonceLength));
        }

        return CreateCipherAndMac(cipher, mac, keys, ivLetter, keyLetter, macLetter);
    }

    /// <summary>
    /// The cipher and HMAC protection of <see cref="Create"/> - AES-CTR, or a weak cipher of
    /// <see cref="SshCipherAlgorithm"/> - keyed with the direction's letters.
    /// </summary>
    /// <returns>The protection, or <see langword="null"/> for a cipher or MAC not built.</returns>
    private static SshCipherAndMacProtection? CreateCipherAndMac(string cipher, string? mac, SshKeyDerivation keys, char ivLetter, char keyLetter, char macLetter)
    {
        if (SshCipherAlgorithm.ForName(cipher) is not { } algorithm || SshHmac.ForName(mac) is not { } hmac)
        {
            return null;
        }

        return new SshCipherAndMacProtection(
            algorithm.Create(keys.DeriveKey(keyLetter, algorithm.KeyLength), keys.DeriveKey(ivLetter, algorithm.InitializationVectorLength)),
            hmac,
            keys.DeriveKey(macLetter, hmac.KeyLength));
    }

    /// <summary>
    /// Gives the first <see cref="HeadLength"/> bytes of a packet in the clear, decrypting them
    /// when <c>packet_length</c> is encrypted.
    /// </summary>
    /// <param name="sequenceNumber">The packet's sequence number.</param>
    /// <param name="head">The bytes as read.</param>
    /// <returns>The same bytes in the clear, <c>packet_length</c> first.</returns>
    public abstract byte[] OpenHead(uint sequenceNumber, byte[] head);

    /// <summary>
    /// Checks the MAC or tag of the rest of a packet and decrypts it.
    /// </summary>
    /// <param name="sequenceNumber">The packet's sequence number.</param>
    /// <param name="plainHead">What <see cref="OpenHead"/> gave.</param>
    /// <param name="rest">The rest of the packet as read, its MAC or tag last.</param>
    /// <returns>The packet in the clear after <c>packet_length</c>: <c>padding_length</c>, the payload and the padding.</returns>
    /// <exception cref="SshDisconnectRequiredException">The MAC or tag does not verify: <c>DISCONNECT</c> 5.</exception>
    public abstract byte[] OpenBody(uint sequenceNumber, byte[] plainHead, byte[] rest);

    /// <summary>
    /// Encrypts a packet and appends its MAC or tag.
    /// </summary>
    /// <param name="sequenceNumber">The packet's sequence number.</param>
    /// <param name="packet">The packet in the clear, <c>packet_length</c> first, padded to <see cref="BlockSize"/>.</param>
    /// <returns>The bytes to send.</returns>
    public abstract byte[] Seal(uint sequenceNumber, byte[] packet);

    /// <summary>
    /// The refusal of a packet whose MAC or AEAD tag does not verify (ADR-0051, decision 9).
    /// </summary>
    /// <param name="sequenceNumber">The packet's sequence number.</param>
    /// <returns>The exception.</returns>
    private protected static SshDisconnectRequiredException MacError(uint sequenceNumber) =>
        new(SshDisconnectReason.MacError, "MAC error", $"The MAC of the client's SSH packet {sequenceNumber} does not verify.");

    private sealed class SshNoPacketProtection : SshPacketProtection
    {
        public override int BlockSize => 8;

        public override bool EncryptsLength => false;

        public override bool AlignsLength => true;

        public override int TagLength => 0;

        public override byte[] OpenHead(uint sequenceNumber, byte[] head) => head;

        public override byte[] OpenBody(uint sequenceNumber, byte[] plainHead, byte[] rest) => rest;

        public override byte[] Seal(uint sequenceNumber, byte[] packet) => packet;
    }
}
