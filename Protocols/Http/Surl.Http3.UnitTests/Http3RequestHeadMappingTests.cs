using Surl.Qpack;

namespace Surl.Http3;

[TestClass]
public sealed class Http3RequestHeadMappingTests
{
    [TestMethod]
    public void Map_WellFormedGet_GivesItsMethodTargetAuthorityAndRegularFields()
    {
        var fields = Fields(":method GET", ":scheme http", ":authority example", ":path /a?b", "accept */*", "content-length 3", "expect 100-Continue");

        var (head, malformation) = Http3RequestHeadMapping.Map(fields);

        Assert.IsNull(malformation);
        Assert.AreEqual("GET", head!.Method);
        Assert.AreEqual("/a?b", head.Target);
        Assert.AreEqual("example", head.Authority);
        Assert.AreEqual(3L, head.ContentLength);
        Assert.IsTrue(head.ExpectsContinue);
        CollectionAssert.AreEqual(new[] { "accept", "content-length", "expect" }, head.Fields.Select(field => field.Key).ToArray());
    }

    [TestMethod]
    [DataRow("host example", "example")]
    [DataRow("accept */*", null)]
    public void Map_NoAuthority_TakesTheOneHostFieldOrNone(string field, string? authority)
    {
        var (head, _) = Http3RequestHeadMapping.Map(Fields(":method GET", ":scheme http", ":path /", field));

        Assert.AreEqual(authority, head!.Authority);
        Assert.IsNull(head.ContentLength);
        Assert.IsFalse(head.ExpectsContinue);
    }

    [TestMethod]
    public void Map_Connect_TargetsItsAuthority()
    {
        var (head, malformation) = Http3RequestHeadMapping.Map(Fields(":method CONNECT", ":authority example:443"));

        Assert.IsNull(malformation);
        Assert.AreEqual("example:443", head!.Target);
    }

    [TestMethod]
    [DataRow("accept */*|:method GET", "the pseudo-header field :method follows a regular field")]
    [DataRow(":method GET|:protocol websocket", "the pseudo-header field :protocol is not one a request may carry")]
    [DataRow(":method GET|:method GET", "the pseudo-header field :method is repeated")]
    [DataRow(":scheme http|:path /", "the request has no :method")]
    [DataRow(":method GET|:path /", "the request needs :scheme and a :path that is not empty")]
    [DataRow(":method GET|:scheme http", "the request needs :scheme and a :path that is not empty")]
    [DataRow(":method CONNECT", "a CONNECT request needs :authority and may carry neither :scheme nor :path")]
    [DataRow(":method CONNECT|:authority a|:scheme http", "a CONNECT request needs :authority and may carry neither :scheme nor :path")]
    [DataRow(":method CONNECT|:authority a|:path /", "a CONNECT request needs :authority and may carry neither :scheme nor :path")]
    [DataRow(":method GET|:scheme http|:path /|content-length 1x", "the content-length is not one number of decimal digits")]
    [DataRow(":method GET|:scheme http|:path /|content-length 1|content-length 2", "the content-length is not one number of decimal digits")]
    [DataRow(":method GET|:scheme http|:path /|content-length 99999999999999999999", "the content-length is not one number of decimal digits")]
    [DataRow(":method GET|:scheme http|:path /|connection close", "the connection-specific field connection is not allowed in HTTP/2 or HTTP/3")]
    [DataRow(":method GET|:scheme http|:path /|te gzip", "the connection-specific field te is not allowed in HTTP/2 or HTTP/3")]
    public void Map_MalformedRequest_NamesWhy(string fieldLines, string expected)
    {
        var (head, malformation) = Http3RequestHeadMapping.Map(Fields(fieldLines.Split('|')));

        Assert.IsNull(head);
        Assert.AreEqual(expected, malformation);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("User-Agent")]
    [DataRow("a b")]
    [DataRow("a\u007F")]
    [DataRow("café")]
    public void Map_FieldNameNotAllowed_IsMalformed(string name)
    {
        var fields = Fields(":method GET", ":scheme http", ":path /").Append(new QpackFieldLine(name, "x")).ToArray();

        var (_, malformation) = Http3RequestHeadMapping.Map(fields);

        Assert.AreEqual("a field name is empty or holds an uppercase letter or a character a field name may not", malformation);
    }

    [TestMethod]
    [DataRow("a\0b")]
    [DataRow("a\rb")]
    [DataRow("a\nb")]
    [DataRow(" a")]
    [DataRow("a\t")]
    public void Map_FieldValueNotAllowed_IsMalformed(string value)
    {
        var fields = Fields(":method GET", ":scheme http", ":path /").Append(new QpackFieldLine("x", value)).ToArray();

        var (_, malformation) = Http3RequestHeadMapping.Map(fields);

        Assert.AreEqual("the x field's value holds NUL, CR or LF, or starts or ends with whitespace", malformation);
    }

    [TestMethod]
    public void Map_TeTrailersAndAnEmptyValue_AreAllowed()
    {
        var (head, _) = Http3RequestHeadMapping.Map(Fields(":method GET", ":scheme http", ":path /", "te trailers", "x "));

        Assert.IsNotNull(head);
    }

    private static QpackFieldLine[] Fields(params string[] lines) =>
        lines.Select(line => line.Split(' ', 2)).Select(parts => new QpackFieldLine(parts[0], parts[1])).ToArray();
}
