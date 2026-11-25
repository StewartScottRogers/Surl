namespace Surl.Cli;

/// <summary>
/// <see cref="WriteOutFormat.Expand"/> against the rules measured from upstream curl 8.21.0 with
/// <c>-w</c> (BL-1663): <c>%%</c>, <c>\r</c> <c>\n</c> <c>\t</c>, other escapes left as they are, an unknown
/// variable printing nothing, an unterminated <c>%{</c> printed as written.
/// </summary>
[TestClass]
public sealed class WriteOutFormatTests
{
    private static readonly WriteOutValues Values =
        new(7, "http", "http://127.0.0.1:8080/", "10.0.0.5", 50000, 120, 33, TimeSpan.FromMilliseconds(1500));

    [TestMethod]
    [DataRow("a%%b\\tc\\nd\\re", "a%b\tc\nd\re", DisplayName = "Percent and control escapes")]
    [DataRow("x%zy\\qz\\\\", "x%zy\\qz\\\\", DisplayName = "Other escapes stay as written")]
    [DataRow("x%{scheme", "x%{scheme", DisplayName = "Unterminated brace stays as written")]
    [DataRow("trailing%", "trailing%", DisplayName = "Lone percent at the end")]
    [DataRow("trailing\\", "trailing\\", DisplayName = "Lone backslash at the end")]
    [DataRow("", "", DisplayName = "Empty format")]
    public void Expand_PlainTextAndEscapes_FollowsUpstreamCurl(string format, string expected) =>
        Assert.AreEqual(expected, WriteOutFormat.Expand(format, Values).Text);

    [TestMethod]
    public void Expand_EachVariable_IsItsValue()
    {
        var result = WriteOutFormat.Expand(
            "%{exchange_id} %{listen_url} %{remote_ip} %{remote_port} %{scheme} %{size_download} %{size_upload} %{time_total}",
            Values);

        Assert.AreEqual("7 http://127.0.0.1:8080/ 10.0.0.5 50000 http 120 33 1.500000", result.Text);
        Assert.IsEmpty(result.UnknownVariables);
    }

    [TestMethod]
    public void Expand_Json_IsEveryVariableAsOneObjectWithEscapedText()
    {
        var values = Values with { ListenUrl = "http://h/\"q\"" };

        Assert.AreEqual(
            "{\"exchange_id\":7,\"listen_url\":\"http://h/\\u0022q\\u0022\",\"remote_ip\":\"10.0.0.5\",\"remote_port\":50000,"
            + "\"scheme\":\"http\",\"size_download\":120,\"size_upload\":33,\"time_total\":1.500000}",
            WriteOutFormat.Expand("%{json}", values).Text);
    }

    [TestMethod]
    public void Expand_UnknownVariables_PrintNothingAndAreReportedInOrder()
    {
        var result = WriteOutFormat.Expand("a%{bogus}b%{scheme}c%{other}", Values);

        Assert.AreEqual("abhttpc", result.Text);
        CollectionAssert.AreEqual(new[] { "bogus", "other" }, result.UnknownVariables.ToArray());
    }

    [TestMethod]
    public void VariableNames_AreEachKnownToExpand()
    {
        foreach (var name in WriteOutFormat.VariableNames)
        {
            Assert.IsEmpty(WriteOutFormat.Expand("%{" + name + "}", Values).UnknownVariables, name);
        }
    }

    [TestMethod]
    public void Expand_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => WriteOutFormat.Expand(null!, Values));
        Assert.Throws<ArgumentNullException>(() => WriteOutFormat.Expand("x", null!));
    }
}
