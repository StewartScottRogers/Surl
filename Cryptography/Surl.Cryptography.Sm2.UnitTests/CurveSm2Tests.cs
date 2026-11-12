using System.Globalization;
using System.Numerics;

namespace Surl.Cryptography.Sm2;

// Every expected point below is copied from GM/T 0003.5-2012 ("Public key cryptographic
// algorithm SM2 based on elliptic curves, Part 5: Parameter definition"), OSCCA's English text,
// http://www.gmbz.org.cn/upload/2018-07-24/1532401863206085511.pdf - Annexes A.2 and B.2, whose
// examples run on the recommended curve. CurveSm2 reads a private key as the scalar
// (value mod (n - 1)) + 1, so a published scalar k is passed as the 32 bytes of k - 1.
[TestClass]
public sealed class CurveSm2Tests
{
    // GM/T 0003.5-2012 Annex A.2: the signer's private key dA and public key PA.
    private const string AnnexADA = "3945208F7B2144B13F36E38AC6D39F95889393692860B51A42FB81EF4DF7C5B8";
    private const string AnnexAPA =
        "09F9DF311E5421A150DD7D161E4BC5C672179FAD1833FC076BB08FF356F35020"
        + "CCEA490CE26775A52DC6EA718CC1AA600AED05FBF35E084A6632F6072DA9AD13";

    // GM/T 0003.5-2012 Annex B.2: the shared point U = V both users reach.
    private const string AnnexBSharedX = "C558B44BEE5301D9F52B44D939BB59584D75B9034DD6A9FC826872109A65739F";

    // GM/T 0003.5-2012 Annex A.2: the random number k and the point (x1, y1) = [k]G.
    [TestMethod]
    [DataRow(
        AnnexADA,
        AnnexAPA,
        DisplayName = "Annex A.2: private key dA and public key PA")]
    [DataRow(
        "59276E27D506861A16680F3AD9C02DCCEF3CC1FA3CDBE4CE6D54B80DEAC1BC21",
        "04EBFC718E8D179862043226" + "8E77FEB6415E2EDE0E073C0F4F640ECD2E149A73"
        + "E858F9D81E5430A57B36DAAB8F950A3C64E6EE6A63094D99283AFF767E124DF0",
        DisplayName = "Annex A.2: random number k and the point [k]G")]
    [DataRow(
        "81EB26E941BB5AF16DF116495F90695272AE2CD63D6C4AE1678418BE48230029",
        "160E12897DF4EDB61DD812FEB96748FBD3CCF4FFE26AA6F6DB9540AF49C94232"
        + "4A7DAD08BB9A459531694BEB20AA489D6649975E1BFCF8C4741B78B4B223007F",
        DisplayName = "Annex B.2: user A's private key dA and public key PA")]
    [DataRow(
        "785129917D45A9EA5437A59356B82338EAADDA6CEB199088F14AE10DEFA229B5",
        "6AE848C57C53C7B1B5FA99EB2286AF078BA64C64591B8B566F7357D576F16DFB"
        + "EE489D771621A27B36C5C7992062E9CD09A9264386F3FBEA54DFF69305621C4D",
        DisplayName = "Annex B.2: user B's private key dB and public key PB")]
    [DataRow(
        "D4DE15474DB74D06491C440D305E012400990F3E390C7E87153C12DB2EA60BB3",
        "64CED1BDBC99D590049B434D0FD73428CF608A5DB8FE5CE07F15026940BAE40E"
        + "376629C7AB21E7DB260922499DDB118F07CE8EAAE3E7720AFEF6A5CC062070C0",
        DisplayName = "Annex B.2 step A1-A3: random number rA and RA = [rA]G")]
    [DataRow(
        "7E07124814B309489125EAED101113164EBF0F3458C5BD88335C1F9D596243D6",
        "ACC27688A6F7B706098BC91FF3AD1BFF7DC2802CDB14CCCCDB0A90471F9BD707"
        + "2FEDAC0494B2FFC4D6853876C79B8F301C6573AD0AA50F39FC87181E1A1B46FE",
        DisplayName = "Annex B.2 step B1-B9: random number rB and RB = [rB]G")]
    public void ComputePublicKey_GmT00035PublishedKeyPair_ReturnsThePublishedPoint(string scalar, string point)
    {
        var publicKey = new byte[CurveSm2.PublicKeySize];

        CurveSm2.ComputePublicKey(PrivateKeyFor(scalar), publicKey);

        Assert.AreEqual("04" + point, Convert.ToHexString(publicKey));
    }

    // The all-zero private key is the scalar (0 mod (n - 1)) + 1 = 1, so its public key is G,
    // whose coordinates GM/T 0003.5-2012 section 2 publishes.
    [TestMethod]
    public void ComputePublicKey_AllZeroPrivateKey_ReturnsTheBasePoint()
    {
        var publicKey = new byte[CurveSm2.PublicKeySize];

        CurveSm2.ComputePublicKey(new byte[CurveSm2.PrivateKeySize], publicKey);

        Assert.AreEqual(
            "04"
            + "32C4AE2C1F1981195F9904466A39C9948FE30BBFF2660BE1715A4589334C74C7"
            + "BC3736A2F4F6779C59BDCEE36B692153D0A9877CC62A474002DF32E52139F0A0",
            Convert.ToHexString(publicKey));
    }

    // GM/T 0003.5-2012 Annex B.2: user B computes V = [tB](PA + [x̄A]RA) and user A computes
    // U = [tA](PB + [x̄B]RB); the annex prints tB, tA, both sums and U = V. Each side is an
    // ECDH of its t with the sum, and both reach the same x-coordinate.
    [TestMethod]
    [DataRow(
        "D0429637F5A6D5D1E6C545235169DF85231163060A654ECBA0F657FD629E8DD9",
        "85C40F88CECA80E38172093FC4BA458188E7C58AF81CF2AF454EC43143E55615"
        + "8C152CB0A131C958C279DEBECC6AB7396A7BC875FC801BB294C284F47F65F6ED",
        DisplayName = "Annex B.2 step B5-B6: user B's tB with PA + [x̄A]RA")]
    [DataRow(
        "3D68C0C06DC40F17B9DDFE0093D3C0E4969ED1124A187FA8AD02F81E3C11CCE6",
        "FE7C111CC3E628E3FE709DF2E6E331CDC2A3A30EEA0CDC3CD10C0759EAB15199"
        + "12D6F496361948C9EC67E603DF93C00886EFAEEAC591C2D5D16B67F2FE1AD77E",
        DisplayName = "Annex B.2 step A4-A5: user A's tA with PB + [x̄B]RB")]
    public void TryComputeSharedSecret_GmT00035KeyExchange_ReturnsTheSharedPointsX(string scalar, string peerPoint)
    {
        var sharedSecret = new byte[CurveSm2.SharedSecretSize];

        var agreed = CurveSm2.TryComputeSharedSecret(PrivateKeyFor(scalar), Convert.FromHexString("04" + peerPoint), sharedSecret);

        Assert.IsTrue(agreed);
        Assert.AreEqual(AnnexBSharedX, Convert.ToHexString(sharedSecret));
    }

    // GM/T 0003.5-2012 Annex B.2 step B5: x̄A = 2^127 + (xA & (2^127 - 1)) of RA, printed as
    // CF608A5D B8FE5CE0 7F150269 40BAE40E, and the point [x̄A]RA the annex prints next.
    [TestMethod]
    public void TryComputeSharedSecret_GmT00035XBarATimesRA_ReturnsThePublishedX()
    {
        var sharedSecret = new byte[CurveSm2.SharedSecretSize];
        var ra = Convert.FromHexString(
            "04"
            + "64CED1BDBC99D590049B434D0FD73428CF608A5DB8FE5CE07F15026940BAE40E"
            + "376629C7AB21E7DB260922499DDB118F07CE8EAAE3E7720AFEF6A5CC062070C0");

        var agreed = CurveSm2.TryComputeSharedSecret(PrivateKeyFor("CF608A5DB8FE5CE07F15026940BAE40E"), ra, sharedSecret);

        Assert.IsTrue(agreed);
        Assert.AreEqual("8D62DAF7DC084E4A85D32214686058545837BDC22D6E9AFE015828A8E1094EC2", Convert.ToHexString(sharedSecret));
    }

    // Property: two parties' ECDH agrees whatever the private keys, including the all-ones key.
    [TestMethod]
    [DataRow("00", "01", DisplayName = "Smallest keys")]
    [DataRow("FF", "A5", DisplayName = "All-ones key and a patterned key")]
    [DataRow("3C", "C3", DisplayName = "Two patterned keys")]
    public void TryComputeSharedSecret_TwoParties_AgreeOnTheSecret(string aliceByte, string bobByte)
    {
        var alicePrivate = Filled(aliceByte);
        var bobPrivate = Filled(bobByte);
        var alicePublic = new byte[CurveSm2.PublicKeySize];
        var bobPublic = new byte[CurveSm2.PublicKeySize];
        var aliceSecret = new byte[CurveSm2.SharedSecretSize];
        var bobSecret = new byte[CurveSm2.SharedSecretSize];
        CurveSm2.ComputePublicKey(alicePrivate, alicePublic);
        CurveSm2.ComputePublicKey(bobPrivate, bobPublic);

        Assert.IsTrue(CurveSm2.TryComputeSharedSecret(alicePrivate, bobPublic, aliceSecret));
        Assert.IsTrue(CurveSm2.TryComputeSharedSecret(bobPrivate, alicePublic, bobSecret));
        Assert.AreEqual(Convert.ToHexString(aliceSecret), Convert.ToHexString(bobSecret));
    }

    // RFC 8446 section 4.2.8.2: a peer point must be 0x04 || x || y with x, y < p on the curve.
    [TestMethod]
    [DataRow(
        "02" + "09F9DF311E5421A150DD7D161E4BC5C672179FAD1833FC076BB08FF356F35020"
        + "CCEA490CE26775A52DC6EA718CC1AA600AED05FBF35E084A6632F6072DA9AD13",
        DisplayName = "Compressed-point prefix 0x02")]
    [DataRow(
        "04" + "FFFFFFFEFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF00000000FFFFFFFFFFFFFFFF"
        + "CCEA490CE26775A52DC6EA718CC1AA600AED05FBF35E084A6632F6072DA9AD13",
        DisplayName = "x equal to p")]
    [DataRow(
        "04" + "09F9DF311E5421A150DD7D161E4BC5C672179FAD1833FC076BB08FF356F35020"
        + "FFFFFFFEFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF00000000FFFFFFFFFFFFFFFF",
        DisplayName = "y equal to p")]
    [DataRow(
        "04" + "09F9DF311E5421A150DD7D161E4BC5C672179FAD1833FC076BB08FF356F35020"
        + "CCEA490CE26775A52DC6EA718CC1AA600AED05FBF35E084A6632F6072DA9AD14",
        DisplayName = "Point off the curve (y + 1)")]
    [DataRow(
        "04" + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000",
        DisplayName = "All-zero coordinates")]
    public void TryComputeSharedSecret_InvalidPeerPoint_ReturnsFalseAndWritesNothing(string peerPublicKey)
    {
        var sharedSecret = Filled("AA");

        var agreed = CurveSm2.TryComputeSharedSecret(PrivateKeyFor(AnnexADA), Convert.FromHexString(peerPublicKey), sharedSecret);

        Assert.IsFalse(agreed);
        Assert.AreEqual(Convert.ToHexString(Filled("AA")), Convert.ToHexString(sharedSecret));
    }

    // A product at the point at infinity is refused: [n]G is infinity because G has order n.
    [TestMethod]
    public void TryComputeSharedSecret_ProductAtInfinity_ReturnsFalseAndWritesNothing()
    {
        var sharedSecret = Filled("AA");

        var agreed = CurveSm2.TryComputeSharedSecret(CurveSm2Parameters.N, CurveSm2Parameters.G, sharedSecret);

        Assert.IsFalse(agreed);
        Assert.AreEqual(Convert.ToHexString(Filled("AA")), Convert.ToHexString(sharedSecret));
    }

    // The scalar is (value mod (n - 1)) + 1: the value n - 1 wraps to 1, and the value n - 2 is
    // the largest scalar, n - 1.
    [TestMethod]
    public void ScalarOf_ValuesAroundNMinusOne_WrapIntoOneToNMinusOne()
    {
        var n = CurveSm2Parameters.N;

        Assert.AreEqual(BigInteger.One, CurveSm2.ScalarOf(BigEndian(n - 1)));
        Assert.AreEqual(n - 1, CurveSm2.ScalarOf(BigEndian(n - 2)));
    }

    [TestMethod]
    [DataRow(CurveSm2.PrivateKeySize - 1, CurveSm2.PublicKeySize, "privateKey", DisplayName = "Short private key")]
    [DataRow(CurveSm2.PrivateKeySize + 1, CurveSm2.PublicKeySize, "privateKey", DisplayName = "Long private key")]
    [DataRow(CurveSm2.PrivateKeySize, CurveSm2.PublicKeySize - 1, "publicKey", DisplayName = "Short public key")]
    public void ComputePublicKey_WrongSize_ThrowsArgumentException(int privateKeySize, int publicKeySize, string parameterName)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => CurveSm2.ComputePublicKey(new byte[privateKeySize], new byte[publicKeySize]));

        Assert.AreEqual(parameterName, exception.ParamName);
    }

    [TestMethod]
    [DataRow(CurveSm2.PrivateKeySize - 1, CurveSm2.PublicKeySize, CurveSm2.SharedSecretSize, "privateKey", DisplayName = "Short private key")]
    [DataRow(CurveSm2.PrivateKeySize, CurveSm2.PublicKeySize - 1, CurveSm2.SharedSecretSize, "peerPublicKey", DisplayName = "Short peer public key")]
    [DataRow(CurveSm2.PrivateKeySize, CurveSm2.PublicKeySize + 1, CurveSm2.SharedSecretSize, "peerPublicKey", DisplayName = "Long peer public key")]
    [DataRow(CurveSm2.PrivateKeySize, CurveSm2.PublicKeySize, CurveSm2.SharedSecretSize - 1, "sharedSecret", DisplayName = "Short shared secret")]
    public void TryComputeSharedSecret_WrongSize_ThrowsArgumentException(int privateKeySize, int peerPublicKeySize, int sharedSecretSize, string parameterName)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => CurveSm2.TryComputeSharedSecret(new byte[privateKeySize], new byte[peerPublicKeySize], new byte[sharedSecretSize]));

        Assert.AreEqual(parameterName, exception.ParamName);
    }

    [TestMethod]
    public void Sizes_AreRfc8446sForA256BitCurve()
    {
        Assert.AreEqual(32, CurveSm2.PrivateKeySize);
        Assert.AreEqual(65, CurveSm2.PublicKeySize);
        Assert.AreEqual(32, CurveSm2.SharedSecretSize);
    }

    /// <summary>The 32 big-endian bytes CurveSm2 reads as <paramref name="scalarHexadecimal" />: k - 1.</summary>
    private static byte[] PrivateKeyFor(string scalarHexadecimal) =>
        BigEndian(BigInteger.Parse("0" + scalarHexadecimal, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture) - 1);

    private static byte[] BigEndian(BigInteger value)
    {
        var bytes = new byte[CurveSm2.PrivateKeySize];
        var length = value.GetByteCount(isUnsigned: true);
        Assert.IsTrue(value.TryWriteBytes(bytes.AsSpan(bytes.Length - length), out _, isUnsigned: true, isBigEndian: true));
        return bytes;
    }

    private static byte[] Filled(string hexadecimalByte)
    {
        var bytes = new byte[CurveSm2.PrivateKeySize];
        Array.Fill(bytes, Convert.FromHexString(hexadecimalByte)[0]);
        return bytes;
    }
}
