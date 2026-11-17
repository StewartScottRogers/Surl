using System.Text;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseTimeoutOutputTests
{
    [TestMethod]
    public void FileNameFor_Platform_IsBesideTheFailuresFileNamePattern()
    {
        Assert.AreEqual("upstream-test-case-timeout-output.linux-x64.txt", UpstreamTestCaseTimeoutOutput.FileNameFor("linux-x64"));
    }

    [TestMethod]
    public void Format_NothingRecorded_IsEmpty()
    {
        Assert.AreEqual(string.Empty, new UpstreamTestCaseTimeoutOutput().Format());
    }

    [TestMethod]
    public void Format_TimedOutCases_WritesStdoutAndStderrInNumberOrder()
    {
        var output = new UpstreamTestCaseTimeoutOutput();
        output.Record(2407, Encoding.Latin1.GetBytes("waitfds 1\r\n"), "* [HTTP/2] [1] OPENED stream\r\n", "http/2: stream 1 opened\r\n");
        output.Record(12, [], string.Empty, string.Empty);

        var text = output.Format();

        Assert.AreEqual(
            "test12 stdout so far (0 bytes):\n\ntest12 stderr so far:\n\ntest12 exchange log notes:\n\n\n"
            + "test2407 stdout so far (11 bytes):\nwaitfds 1\ntest2407 stderr so far:\n* [HTTP/2] [1] OPENED stream\n"
            + "test2407 exchange log notes:\nhttp/2: stream 1 opened\n\n",
            text);
    }
}
