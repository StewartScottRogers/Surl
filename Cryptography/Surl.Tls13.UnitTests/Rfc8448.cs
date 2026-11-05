namespace Surl.Tls13;

/// <summary>
/// The worked handshakes of RFC 8448, "Example Handshake Traces for TLS 1.3": section 3 (Simple
/// 1-RTT Handshake) and the messages of section 5 (HelloRetryRequest), copied byte for byte from
/// https://www.rfc-editor.org/rfc/rfc8448.txt. Every expected value in this project's RFC tests
/// comes from here (ADR-0003: the specification, never the Curl port).
/// </summary>
internal static class Rfc8448
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

    public static readonly byte[] EncryptedExtensions = Convert.FromHexString(
        "080000240022000a00140012001d00170018001901000101010201030104001c0002400100000000");

    public static readonly byte[] Certificate = Convert.FromHexString(
        "0b0001b9000001b50001b0308201ac30820115a003020102020102300d06092a864886f70d01010b0500300e310c300a" +
        "06035504031303727361301e170d3136303733303031323335395a170d3236303733303031323335395a300e310c300a" +
        "0603550403130372736130819f300d06092a864886f70d010101050003818d0030818902818100b4bb498f8279303d98" +
        "0836399b36c6988c0c68de55e1bdb826d3901a2461eafd2de49a91d015abbc9a95137ace6c1af19eaa6af98c7ced4312" +
        "0998e187a80ee0ccb0524b1b018c3e0b63264d449a6d38e22a5fda430846748030530ef0461c8ca9d9efbfae8ea6d1d0" +
        "3e2bd193eff0ab9a8002c47428a6d35a8d88d79f7f1e3f0203010001a31a301830090603551d1304023000300b060355" +
        "1d0f0404030205a0300d06092a864886f70d01010b05000381810085aad2a0e5b9276b908c65f73a7267170618a54c5f" +
        "8a7b337d2df7a594365417f2eae8f8a58c8f8172f9319cf36b7fd6c55b80f21a03015156726096fd335e5e67f2dbf102" +
        "702e608ccae6bec1fc63a42a99be5c3eb7107c3c54e9b9eb2bd5203b1c3b84e0a8b2f759409ba3eac9d91d402dcc0cc8" +
        "f8961229ac9187b42b4de10000");

    public static readonly byte[] CertificateVerify = Convert.FromHexString(
        "0f000084080400805a747c5d88fa9bd2e55ab085a61015b7211f824cd484145ab3ff52f1fda8477b0b7abc90db78e2d3" +
        "3a5c141a078653fa6bef780c5ea248eeaaa785c4f394cab6d30bbe8d4859ee511f602957b15411ac027671459e46445c" +
        "9ea58c181e818e95b8c3fb0bf3278409d3be152a3da5043e063dda65cdf5aea20d53dfacd42f74f3");

    public static readonly byte[] ServerFinished = Convert.FromHexString(
        "140000209b9b141d906337fbd2cbdce71df4deda4ab42c309572cb7fffee5454b78f0718");

    public static readonly byte[] ClientFinished = Convert.FromHexString(
        "14000020a8ec436d677634ae525ac1fcebe11a039ec17694fac6e98527b642f2edd5ce61");

    public static readonly byte[] SharedSecret = Convert.FromHexString(
        "8bd4054fb55b9d63fdfbacf9f04b9f0d35e6d63f537563efd46272900f89492d");

    public static readonly byte[] EarlySecret = Convert.FromHexString(
        "33ad0a1c607ec03b09e6cd9893680ce210adf300aa1f2660e1b22e10f170f92a");

    public static readonly byte[] HandshakeSecret = Convert.FromHexString(
        "1dc826e93606aa6fdc0aadc12f741b01046aa6b99f691ed221a9f0ca043fbeac");

    public static readonly byte[] MasterSecret = Convert.FromHexString(
        "18df06843d13a08bf2a449844c5f8a478001bc4d4c627984d5a41da8d0402919");

    public static readonly byte[] ClientHandshakeTrafficSecret = Convert.FromHexString(
        "b3eddb126e067f35a780b3abf45e2d8f3b1a950738f52e9600746a0e27a55a21");

    public static readonly byte[] ServerHandshakeTrafficSecret = Convert.FromHexString(
        "b67b7d690cc16c4e75e54213cb2d37b4e9c912bcded9105d42befd59d391ad38");

    public static readonly byte[] HelloTranscriptHash = Convert.FromHexString(
        "860c06edc07858ee8e78f0e7428c58edd6b43f2ca3e6e95f02ed063cf0e1cad8");

    public static readonly byte[] ServerFinishedKey = Convert.FromHexString(
        "008d3b66f816ea559f96b537e885c31fc068bf492c652f01f288a1d8cdc19fc8");

    public static readonly byte[] ServerVerifyData = Convert.FromHexString(
        "9b9b141d906337fbd2cbdce71df4deda4ab42c309572cb7fffee5454b78f0718");

    public static readonly byte[] ServerFinishedTranscriptHash = Convert.FromHexString(
        "9608102a0f1ccc6db6250b7b7e417b1a000eaada3daae4777a7686c9ff83df13");

    public static readonly byte[] ClientApplicationTrafficSecret = Convert.FromHexString(
        "9e40646ce79a7f9dc05af8889bce6552875afa0b06df0087f792ebb7c17504a5");

    public static readonly byte[] ServerApplicationTrafficSecret = Convert.FromHexString(
        "a11af9f05531f856ad47116b45a950328204b4f44bfb6b3a4b4f1f3fcb631643");

    public static readonly byte[] ClientFinishedKey = Convert.FromHexString(
        "b80ad01015fb2f0bd65ff7d4da5d6bf83f84821d1f87fdc7d3c75b5a7b42d9c4");

    public static readonly byte[] ClientVerifyData = Convert.FromHexString(
        "a8ec436d677634ae525ac1fcebe11a039ec17694fac6e98527b642f2edd5ce61");

    public static readonly byte[] ServerHandshakeKey = Convert.FromHexString(
        "3fce516009c21727d0f2e4e86ee403bc");

    public static readonly byte[] ServerHandshakeIv = Convert.FromHexString(
        "5d313eb2671276ee13000b30");

    public static readonly byte[] Section5ClientHello1 = Convert.FromHexString(
        "010000b00303b0b1c5a5aa37c5919f2ed1d5c6fff7fcb7849716945a2b8cee9258a346677b6f00000613011303130201" +
        "0000810000000b0009000006736572766572ff01000100000a00080006001d00170018003300260024001d0020e8e8e3" +
        "f3b93a25ed97a14a7dcacb8a272c6288e585c6484d05262fcad062ad1f002b0003020304000d0020001e040305030603" +
        "020308040805080604010501060102010402050206020202002d00020101001c00024001");

    public static readonly byte[] Section5HelloRetryRequest = Convert.FromHexString(
        "020000ac0303cf21ad74e59a6111be1d8c021e65b891c2a211167abb8c5e079e09e2c8a8339c00130100008400330002" +
        "0017002c0074007271dcd04bb88bc3189119398a00000000eefafc76c146b823b096f8aacad365dd0030953f4edf6256" +
        "36e5f21bb2e23fcc654b1b5b40318d10d137abcbb87574e36e8a1f025f7dfa5d6e50781b5eda4aa15b0c8be778257d16" +
        "aa3030e9e7841dd9e4c0342267e8ca0caf571fb2b7cff0f934b0002b00020304");

    public static readonly byte[] Section5ClientHello2 = Convert.FromHexString(
        "010001fc0303b0b1c5a5aa37c5919f2ed1d5c6fff7fcb7849716945a2b8cee9258a346677b6f00000613011303130201" +
        "0001cd0000000b0009000006736572766572ff01000100000a00080006001d001700180033004700450017004104a6da" +
        "7392ec591e17abfd535964b99894d13befb221b3def2ebe3830eac8f0151812677c4d6d2237e85cf01d6910cfb83954e" +
        "76ba7352830534159897e8065780002b0003020304000d0020001e040305030603020308040805080604010501060102" +
        "010402050206020202002c0074007271dcd04bb88bc3189119398a00000000eefafc76c146b823b096f8aacad365dd00" +
        "30953f4edf625636e5f21bb2e23fcc654b1b5b40318d10d137abcbb87574e36e8a1f025f7dfa5d6e50781b5eda4aa15b" +
        "0c8be778257d16aa3030e9e7841dd9e4c0342267e8ca0caf571fb2b7cff0f934b0002d00020101001c00024001001500" +
        "af0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
        "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
        "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
        "0000000000000000000000000000000000000000000000000000000000000000");

    public static readonly byte[] Section5ServerHello = Convert.FromHexString(
        "020000770303bb341d847fd789c47c387172dc0c9bf147fccacb5043d86ca4c598d3ff571b9800130100004f00330045" +
        "0017004104583e054b7a66672ae020ad9d2686fcc85b5ad41a134a0f03ee72b893052bd85b4c8de6776f5b04ac07d835" +
        "40eab3e3d9c547bc6528c4317d294686093a6cad7d002b00020304");

    public static readonly byte[] Section5HelloTranscriptHash = Convert.FromHexString(
        "8aa8e828ec2f8a884fec95a3139de01c15a3daa7ff5bfc3f4bfcc21b438d7bf8");
}
