using static Surl.Conformance.UpstreamTestCaseText;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCasePartTests
{
    [TestMethod]
    public void Content_IsTheLinesAsBytes()
    {
        var part = Part(Attributes(), "a\n", "ÿ\n");

        CollectionAssert.AreEqual(new byte[] { (byte)'a', 10, 0xff, 10 }, part.Content);
    }

    [TestMethod]
    public void ContentWithNewlineAttributes_NoAttributes_IsTheContent()
    {
        var part = Part(Attributes(), "a\n", "b\n");

        Assert.AreEqual("a\nb\n", Apply(part, UpstreamTestCaseNewlineRule.CutThenCrlf));
    }

    [TestMethod]
    public void ContentWithNewlineAttributes_Nonewline_CutsOnlyTheFinalNewline()
    {
        var part = Part(Attributes(("nonewline", "yes")), "a\n", "b\n");

        Assert.AreEqual("a\nb", Apply(part, UpstreamTestCaseNewlineRule.CutThenCrlf));
    }

    [TestMethod]
    public void ContentWithNewlineAttributes_NonewlineOnLastLineWithoutOne_IsUnchanged()
    {
        var part = Part(Attributes(("nonewline", "yes")), "a\n", "b");

        Assert.AreEqual("a\nb", Apply(part, UpstreamTestCaseNewlineRule.CutThenCrlf));
    }

    [TestMethod]
    public void ContentWithNewlineAttributes_NonewlineOnEmptyPart_IsEmpty()
    {
        var part = Part(Attributes(("nonewline", "yes")));

        Assert.IsEmpty(part.ContentWithNewlineAttributes(UpstreamTestCaseNewlineRule.CutThenCrlf));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("0")]
    public void ContentWithNewlineAttributes_AttributesPerlCountsFalse_AreNotApplied(string value)
    {
        var part = Part(Attributes(("nonewline", value), ("crlf", value)), "a\n");

        Assert.AreEqual("a\n", Apply(part, UpstreamTestCaseNewlineRule.CutThenCrlf));
    }

    [TestMethod]
    public void ContentWithNewlineAttributes_NonewlineAnyOtherValue_CutsTheNewline()
    {
        var part = Part(Attributes(("nonewline", "no")), "a\n");

        Assert.AreEqual("a", Apply(part, UpstreamTestCaseNewlineRule.CutThenCrlf));
    }

    [TestMethod]
    public void ContentWithNewlineAttributes_CrlfYes_MakesEveryNewlineCrlf()
    {
        var part = Part(Attributes(("crlf", "yes")), "GET / HTTP/1.1\n", "\n", "body\n");

        Assert.AreEqual("GET / HTTP/1.1\r\n\r\nbody\r\n", Apply(part, UpstreamTestCaseNewlineRule.CutThenCrlf));
    }

    [TestMethod]
    public void ContentWithNewlineAttributes_CrlfHeaders_ConvertsOnlyTheHeaderPart()
    {
        var part = Part(Attributes(("crlf", "headers")), "GET / HTTP/1.1\n", "Host: x\n", "\n", "body\n", "\n");

        Assert.AreEqual("GET / HTTP/1.1\r\nHost: x\r\n\r\nbody\n\n", Apply(part, UpstreamTestCaseNewlineRule.CutThenCrlf));
    }

    [TestMethod]
    public void ContentWithNewlineAttributes_CutThenForceCrlf_ForcesEveryLineEvenForHeaders()
    {
        var part = Part(Attributes(("crlf", "headers")), "Host: x\n", "body\n");

        Assert.AreEqual("Host: x\r\nbody\r\n", Apply(part, UpstreamTestCaseNewlineRule.CutThenForceCrlf));
    }

    [TestMethod]
    public void ContentWithNewlineAttributes_CutThenCrlf_LeavesTheLastLineWithoutNewline()
    {
        var part = Part(Attributes(("crlf", "yes"), ("nonewline", "yes")), "a\n", "b\n");

        Assert.AreEqual("a\r\nb", Apply(part, UpstreamTestCaseNewlineRule.CutThenCrlf));
    }

    [TestMethod]
    public void ContentWithNewlineAttributes_CrlfThenCut_LeavesTheCarriageReturn()
    {
        var part = Part(Attributes(("crlf", "yes"), ("nonewline", "yes")), "a\n", "b\n");

        Assert.AreEqual("a\r\nb\r", Apply(part, UpstreamTestCaseNewlineRule.CrlfThenCut));
    }

    [TestMethod]
    public void ContentWithNewlineAttributes_CrlfThenCutWithHeaders_GuessesTheHeaders()
    {
        var part = Part(Attributes(("crlf", "headers")), "Host: x\n", "body\n");

        Assert.AreEqual("Host: x\r\nbody\n", Apply(part, UpstreamTestCaseNewlineRule.CrlfThenCut));
    }

    [TestMethod]
    public void HasAttribute_AbsentOrOtherValue_IsFalse()
    {
        var part = Part(Attributes(("mode", "text")));

        Assert.IsTrue(part.HasAttribute("mode", "text"));
        Assert.IsFalse(part.HasAttribute("mode", "warn"));
        Assert.IsFalse(part.HasAttribute("crlf", "yes"));
    }

    [TestMethod]
    public void IsSet_AbsentEmptyOrZero_IsFalse()
    {
        var part = Part(Attributes(("a", ""), ("b", "0"), ("c", "yes")));

        Assert.IsFalse(part.IsSet("a"));
        Assert.IsFalse(part.IsSet("b"));
        Assert.IsTrue(part.IsSet("c"));
        Assert.IsFalse(part.IsSet("d"));
    }

    private static string Apply(UpstreamTestCasePart part, UpstreamTestCaseNewlineRule rule) =>
        Text(part.ContentWithNewlineAttributes(rule));

    private static Dictionary<string, string> Attributes(params (string Name, string Value)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

    private static UpstreamTestCasePart Part(Dictionary<string, string> attributes, params string[] lines) =>
        new("stdout", attributes, lines);
}
