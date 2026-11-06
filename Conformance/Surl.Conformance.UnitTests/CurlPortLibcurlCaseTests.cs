namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortLibcurlCaseTests
{
    private static readonly CurlPortLibcurlCase Case = new(
        "Text_Echo_ComesBackWhole",
        "PinnedLibcurlTalksToSurlOverWebSocketTests.Text_Echo_ComesBackWhole",
        "Run-LibcurlWebSocketScript.cs",
        ["ws://{surl}/echo", "send:TEXT:hello", "recv"],
        _ => null);

    [TestMethod]
    public void ArgumentsFor_Authority_ReplacesThePlaceholder()
    {
        var arguments = Case.ArgumentsFor("127.0.0.1:50123");

        CollectionAssert.AreEqual(new[] { "ws://127.0.0.1:50123/echo", "send:TEXT:hello", "recv" }, arguments.ToArray());
    }

    [TestMethod]
    public void ArgumentsFor_NoAuthority_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Case.ArgumentsFor(" "));
    }

    [TestMethod]
    public void CommandLine_IsTheUpstreamDriverAndItsArgumentsWithThePlaceholder()
    {
        Assert.AreEqual("Run-LibcurlWebSocketScript.cs ws://{surl}/echo send:TEXT:hello recv", Case.CommandLine);
    }

    [TestMethod]
    public void ComparedStoredFiles_ByDefault_IsEmpty()
    {
        Assert.IsEmpty(Case.ComparedStoredFiles);
    }
}
