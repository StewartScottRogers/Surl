namespace Surl.Tls13Stream;

/// <summary>
/// Values of RFC 8448, "Example Handshake Traces for TLS 1.3", copied byte for byte from
/// https://www.rfc-editor.org/rfc/rfc8448.txt (ADR-0003: the specification, never the Curl port):
/// section 3's ClientHello, the client's and the server's x25519 private keys and ServerHello, and
/// section 5's HelloRetryRequest random.
/// </summary>
internal static class UnitTestRfc8448
{
    public static readonly byte[] ClientHello = Convert.FromHexString(
        "010000c00303cb34ecb1e78163ba1c38c6dacb196a6dffa21a8d9912ec18a2ef6283024dece700000613011303130201" +
        "0000910000000b0009000006736572766572ff01000100000a00140012001d0017001800190100010101020103010400" +
        "230000003300260024001d002099381de560e4bd43d23d8e435a7dbafeb3c06e51c13cae4d5413691e529aaf2c002b00" +
        "03020304000d0020001e040305030603020308040805080604010501060102010402050206020202002d00020101001c" +
        "00024001");

    public static readonly byte[] ServerHello = Convert.FromHexString(
        "020000560303a6af06a4121860dc5e6e60249cd34c95930c8ac5cb1434dac155772ed3e2692800130100002e00330024" +
        "001d0020c9828876112095fe66762bdbf7c672e156d6cc253b833df1dd69b1b04e751f0f002b00020304");

    public static readonly byte[] ClientEphemeralPrivateKey = Convert.FromHexString(
        "49af42ba7f7994852d713ef2784bcbcaa7911de26adc5642cb634540e7ea5005");

    public static readonly byte[] ServerEphemeralPrivateKey = Convert.FromHexString(
        "b1580eeadf6dd589b8ef4f2d5652578cc810e9980191ec8d058308cea216a21e");

    /// <summary>Section 3's ServerHello random, bytes 6 to 37 of <see cref="ServerHello"/>.</summary>
    public static byte[] ServerRandom => ServerHello[6..38];

    /// <summary>The special random value that marks a ServerHello as a HelloRetryRequest (RFC 8446 section 4.1.3).</summary>
    public static readonly byte[] HelloRetryRequestRandom = Convert.FromHexString(
        "cf21ad74e59a6111be1d8c021e65b891c2a211167abb8c5e079e09e2c8a8339c");
}
