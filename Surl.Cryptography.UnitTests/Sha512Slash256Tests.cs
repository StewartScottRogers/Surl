using System.Text;

namespace Surl.Cryptography;

/// <summary>
/// Pins <see cref="Sha512Slash256" /> to hashes published by NIST, never to hashes the
/// code under test computed.
/// </summary>
[TestClass]
public sealed class Sha512Slash256Tests
{
    // NIST CSRC, "Examples with Intermediate Values", SHA-512/256
    // (https://csrc.nist.gov/CSRC/media/Projects/Cryptographic-Standards-and-Guidelines/documents/examples/SHA512_256.pdf).
    [TestMethod]
    [DataRow("abc", "53048e2681941ef99b2e29b76b4c7dabe4c2d0c634fc6d46e0e2f13107e7af23", DisplayName = "abc: one block")]
    [DataRow(
        "abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu",
        "3928e184fb8690f840da3988121d31be65cb9d3ef83ee6146feac861e19b563a",
        DisplayName = "896-bit message: padding needs a second block")]
    public void HashData_NistExample_GivesTheExampleHash(string message, string expected)
    {
        byte[] hash = Sha512Slash256.HashData(Encoding.ASCII.GetBytes(message));

        Assert.AreEqual(expected, Convert.ToHexStringLower(hash));
    }

    // NIST CAVP SHAVS byte-oriented response file SHA512_256ShortMsg.rsp (CAVS 21.1, 2017-07-10),
    // from shabytetestvectors.zip; Len is in bits, and Len = 0 carries Msg = 00 but hashes no bytes.
    [TestMethod]
    [DataRow("", "c672b8d1ef56ed28ab87c3622c5114069bdd3ad7b8f9737498d0c01ecef0967a", DisplayName = "Len = 0: empty message")]
    [DataRow(
        "9607cca45873add19a93dccf3d0f790e856ff30b84c8211ad69b8e628ffa142972ecac5264138423208c524c2b17e9250b780938b41d7cff43005eefeecfbdb53b4b62bf71eeedfe4cd028eaf5cb95c731dd4927d9cffdb18a1463209df4b68f5aea95f3684a11e9882605b28473",
        "73b4086d690ace6940c01912acb3a57bfc15c4a16c40a90b4329f1fa9f3085d2",
        DisplayName = "Len = 880: 110 bytes")]
    [DataRow(
        "5731f467c5b923c43af9c5fa849aad21ab8dd7db1ca1a687065571b705ea3ee4febdcd614ad4d98e16b79a4e09818ebb28367918f757ab06e1b481fbda822ef143adbb5b0e704d5d2222a73c0153ef14a817b5c9b7a2313fd115ccce4698e3f0efa9c73d5ab3089a27e3f3adb23759",
        "f5253b5c69db9c724aebf762ec51c221f8a4d4e2174a4b7f56e4d69aa44adfa3",
        DisplayName = "Len = 888: 111 bytes, the longest that pads into one block")]
    [DataRow(
        "92b23c0bc4d8d07d22e28812710dff06cb9bbecea2c960ac0200f480164fa2e1ee19926c7f0b095cec51d55c040aec990bf9501abd7d355490c366f93a3ae5127347d14dfc3b8d98e0821feefa1cd671b75230ba1da1fa6d0cfbb910c42f491da8a5c455424ea65886db2e735b2d07b9",
        "55a0597f11ff71c426201715beb585f254bb31c1dbade533f04e499c3391ff79",
        DisplayName = "Len = 896: 112 bytes, the shortest that pads into two blocks")]
    [DataRow(
        "bcc51ea0a66564a171dabfa279e384f4d9fdcba38028215788ee7d78c3a2769596e6b2070a6fa2d1200d6ccb65e52900c7015154a70c736a2f562ac4e61f4c2c81116453fd0e63b9ea2c92cc0afecb541a16e90ef0c77d97c630c38cd675d4f027501ccea6c90f1f784118ed8fb5d2b97b",
        "c16ca79c0ab44f39da1c65e8943ad2e90888c3d80b5e3b3b1bff59408b59d6f3",
        DisplayName = "Len = 904: 113 bytes")]
    [DataRow(
        "3e3a52d3261e1194249786d6c0e18d52d92f1c7639f079c26c51aa72d1032e5df13eea1d1006667002ad39de4099c29c3b4719b1f0904557bd2bb0a47374d869ac6b465b5f00c470b18ecb8c0ea53b5d790c4e832006cff534d587a0f77df95117ca4fd43a94935eda422228538d5e5d3a87a436f1db7e63785619ae86a6f9",
        "b34e72cefefb63d6e309bcfb4f0b1d350f2c5c582de3b93ad137f921a92a7e79",
        DisplayName = "Len = 1016: 127 bytes, one short of a whole block")]
    [DataRow(
        "bc8173c878ca60e9a0f823f9a589d4ff84547b389b117fb6bb1b614e7e75a9b1db0b21d9f73b42a73e94eccab3de5ae2845a54e5e24ba6c20fb4d245b964023b863040d6f080e953530d5fd944e8ffa525bf5364f65c88e06e6e22df4b8cee48e67738880a9f3f3406e9e6f001b0ac8f8e0ade7c814c0c5800d0b9e4ddf55622",
        "f691d01ee9ab675f3872313b77e6a4543c71e3e89aa94c48f91d6ee7fa1ab4fb",
        DisplayName = "Len = 1024: 128 bytes, exactly one whole block")]
    public void HashData_CavpShortMessage_GivesTheResponseFileHash(string messageHex, string expected)
    {
        byte[] hash = Sha512Slash256.HashData(Convert.FromHexString(messageHex));

        Assert.AreEqual(expected, Convert.ToHexStringLower(hash));
    }

    // NIST CAVP SHAVS byte-oriented response file SHA512_256LongMsg.rsp (CAVS 21.1, 2017-07-10),
    // from shabytetestvectors.zip; the first entry, Len = 1816: 227 bytes, one whole block and a 99-byte tail.
    [TestMethod]
    public void HashData_CavpLongMessage_GivesTheResponseFileHash()
    {
        const string messageHex =
            "97e003903bb971a523ce0c82bda5d6733c76b90deb307559c1bddd35368743f6563b315214cd5a7ee0bccf937c9776360bc0b9786b707bfbc4fb50576155edbbbfd5ddd8e43a76faf2ec0c78fc84644f188d6b0ab68c28e5303ff031a223d9fafb3871e85408af6381e629fae67488068c68398a758f665e2c12258d9ff8effb31ec534b0c40ebffb43390e1e26fcaa28fd68ac24f7e1cafe0fa573103dc17058a77edc9b3ea1418b45aa7f5977e126d4861c778ed6332217581eee674d739622e63a529f10c11f4a9e3d8feaea848ade0905675f6458ffa132f52749af23d584438e5";

        byte[] hash = Sha512Slash256.HashData(Convert.FromHexString(messageHex));

        Assert.AreEqual("00ce3b592d4e1a65f780df351fa7b2c01b49df4ea913c3fab24297f5791b18e5", Convert.ToHexStringLower(hash));
    }
}
