using System.Text;

namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortResultNormaliserTests
{
    [TestMethod]
    public void NormaliseOutput_ReplacesTheSurlPortOnlyAsAWholeNumber()
    {
        Assert.AreEqual(
            "127.0.0.1:<port>/ 150101 501010 <port>",
            Output("127.0.0.1:50101/ 150101 501010 50101", 50101, []));
    }

    [TestMethod]
    public void NormaliseOutput_ReplacesDateAndLastModifiedValuesInAnyCase()
    {
        Assert.AreEqual(
            "Date: <date>\r\nlast-modified:<date>\r\nX-Date: kept\r\n",
            Output("Date: Wed, 04 Nov 2026 10:00:00 GMT\r\nlast-modified:Tue\r\nX-Date: kept\r\n", 1, []));
    }

    [TestMethod]
    public void NormaliseOutput_ReplacesOnlyTheWriteOutValuesThatMeasureTime()
    {
        string[] arguments = ["--write-out", "%{http_code}|%{time_connect}|%{speed_upload}%%\\t\\r\\n", "{surl}/"];

        Assert.AreEqual(
            "body 0.5\n200|<time>|<time>%\t\r\n",
            Output("body 0.5\n200|0.000321|12345%\t\r\n", 1, arguments));
    }

    [TestMethod]
    public void NormaliseOutput_WriteOutWithNoTimedVariable_OrFromAFile_OrWithNoFormat_IsLeftAlone()
    {
        Assert.AreEqual("200 0.000321", Output("200 0.000321", 1, ["-w", "%{http_code} %{size_download}"]));
        Assert.AreEqual("0.000321", Output("0.000321", 1, ["-w", "@format.txt"]));
        Assert.AreEqual("0.000321", Output("0.000321", 1, ["-w"]));
    }

    [TestMethod]
    public void NormaliseOutput_KeepsEveryOtherByte()
    {
        byte[] output = [0x00, 0xFF, 0x80, 0x0A];

        CollectionAssert.AreEqual(output, CurlPortResultNormaliser.NormaliseOutput(output, 1, []));
    }

    [TestMethod]
    public void NormaliseOutput_OfASurlSide_ReplacesBothTheRelaysPortAndSurlsOwn()
    {
        var side = new CurlPortSurlSide(50101, string.Empty, new Dictionary<string, byte[]>()) { RelayPort = 50202 };
        var output = Encoding.Latin1.GetBytes("0file.txt\t/file.txt\t127.0.0.1\t50101\r\nvia 50202\r\n");

        Assert.AreEqual(
            "0file.txt\t/file.txt\t127.0.0.1\t<port>\r\nvia <port>\r\n",
            Encoding.Latin1.GetString(CurlPortResultNormaliser.NormaliseOutput(output, side, [])));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortResultNormaliser.NormaliseOutput(null!, side, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortResultNormaliser.NormaliseOutput(output, (CurlPortSurlSide)null!, []));
    }

    [TestMethod]
    public void NormaliseStandardError_KeepsOnlyAssertedLines_Normalised()
    {
        Assert.AreEqual(
            "curl: (7) Failed to connect to 127.0.0.1 port <port>\n",
            CurlPortResultNormaliser.NormaliseStandardError(
                "* wording\r\ncurl: (7) Failed to connect to 127.0.0.1 port 50101\r\n", 50101, [], ["curl: (7)"]));
    }

    [TestMethod]
    public void NormaliseSurlNotes_ReplacesPortsBesideLoopbackAddressesAndTimeStamps()
    {
        Assert.AreEqual(
            "<time> 127.0.0.1:<port> [::1]:<port> localhost port <port> <port> 8080\n",
            CurlPortResultNormaliser.NormaliseSurlNotes("10:00:00.123456 127.0.0.1:61000 [::1]:61001 localhost port 61002 50101 8080\n", 50101));
    }

    [TestMethod]
    public void Normalise_NullArguments_ThrowArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortResultNormaliser.NormaliseOutput(null!, 1, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortResultNormaliser.NormaliseOutput([], 1, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortResultNormaliser.NormaliseStandardError(null!, 1, [], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortResultNormaliser.NormaliseStandardError(string.Empty, 1, [], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortResultNormaliser.NormaliseSurlNotes(null!, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortResultNormaliser.NormaliseRequestBytes(null!, 1));
    }

    private static string Output(string output, int surlPort, IReadOnlyList<string> arguments) =>
        Encoding.Latin1.GetString(CurlPortResultNormaliser.NormaliseOutput(Encoding.Latin1.GetBytes(output), surlPort, arguments));
}
