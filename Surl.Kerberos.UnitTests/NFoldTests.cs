using System.Text;

namespace Surl.Kerberos;

/// <summary>
/// Pins <see cref="NFold" /> to every n-fold vector of RFC 3961 appendix A.1, never to values the
/// code under test computed.
/// </summary>
[TestClass]
public sealed class NFoldTests
{
    // RFC 3961, appendix A.1, "n-fold": the test cases of Marc Horowitz and Simon Josefsson, then
    // the folds of "kerberos".
    [TestMethod]
    [DataRow("012345", 64, "be072631276b1955", DisplayName = "64-fold(\"012345\")")]
    [DataRow("password", 56, "78a07b6caf85fa", DisplayName = "56-fold(\"password\")")]
    [DataRow("Rough Consensus, and Running Code", 64, "bb6ed30870b7f0e0", DisplayName = "64-fold(\"Rough Consensus, and Running Code\")")]
    [DataRow("password", 168, "59e4a8ca7c0385c3c37b3f6d2000247cb6e6bd5b3e", DisplayName = "168-fold(\"password\")")]
    [DataRow(
        "MASSACHVSETTS INSTITVTE OF TECHNOLOGY",
        192,
        "db3b0d8f0b061e603282b308a50841229ad798fab9540c1b",
        DisplayName = "192-fold(\"MASSACHVSETTS INSTITVTE OF TECHNOLOGY\")")]
    [DataRow("Q", 168, "518a54a215a8452a518a54a215a8452a518a54a215", DisplayName = "168-fold(\"Q\")")]
    [DataRow("ba", 168, "fb25d531ae8974499f52fd92ea9857c4ba24cf297e", DisplayName = "168-fold(\"ba\")")]
    [DataRow("kerberos", 64, "6b65726265726f73", DisplayName = "64-fold(\"kerberos\")")]
    [DataRow("kerberos", 128, "6b65726265726f737b9b5b2b93132b93", DisplayName = "128-fold(\"kerberos\")")]
    [DataRow("kerberos", 168, "8372c236344e5f1550cd0747e15d62ca7a5a3bcea4", DisplayName = "168-fold(\"kerberos\")")]
    [DataRow(
        "kerberos",
        256,
        "6b65726265726f737b9b5b2b93132b935c9bdcdad95c9899c4cae4dee6d6cae4",
        DisplayName = "256-fold(\"kerberos\")")]
    public void Fold_Rfc3961AppendixA1_GivesTheRfcFold(string input, int outputBits, string expected)
    {
        byte[] folded = NFold.Fold(Encoding.ASCII.GetBytes(input), outputBits / 8);

        Assert.AreEqual(expected, Convert.ToHexStringLower(folded));
    }
}
