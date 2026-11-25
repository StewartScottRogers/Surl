namespace Surl.Cookies;

[TestClass]
public sealed class SetCookieHeaderWriterTests
{
    [TestMethod]
    public void WriteHeaderLine_AdrMeasuredFirstCookie_IsByteForByte()
    {
        CookieDefinition cookie = new("sid", "abc123", "127.0.0.1", "/", "Wed, 09 Jun 2027 10:18:14 GMT", 3600, true, true, "Lax");

        Assert.AreEqual(
            "Set-Cookie: sid=abc123; Domain=127.0.0.1; Path=/; Expires=Wed, 09 Jun 2027 10:18:14 GMT; Max-Age=3600; Secure; HttpOnly; SameSite=Lax",
            SetCookieHeaderWriter.WriteHeaderLine(cookie));
    }

    [TestMethod]
    public void WriteHeaderLine_AdrMeasuredSecondCookie_IsByteForByte()
    {
        Assert.AreEqual("Set-Cookie: sess=xyz; Path=/", SetCookieHeaderWriter.WriteHeaderLine(new CookieDefinition("sess", "xyz", Path: "/")));
    }

    [TestMethod]
    public void WriteFieldValue_NoAttributes_IsNameEqualsValueOnly()
    {
        Assert.AreEqual("a=b", SetCookieHeaderWriter.WriteFieldValue(new CookieDefinition("a", "b")));
    }

    [TestMethod]
    public void WriteFieldValue_EachAttributeAlone_IsWrittenWithItsSpelling()
    {
        Assert.AreEqual("a=b; Domain=example.com", SetCookieHeaderWriter.WriteFieldValue(new CookieDefinition("a", "b", Domain: "example.com")));
        Assert.AreEqual("a=b; Path=/x", SetCookieHeaderWriter.WriteFieldValue(new CookieDefinition("a", "b", Path: "/x")));
        Assert.AreEqual("a=b; Expires=Wed, 09 Jun 2027 10:18:14 GMT", SetCookieHeaderWriter.WriteFieldValue(new CookieDefinition("a", "b", Expires: "Wed, 09 Jun 2027 10:18:14 GMT")));
        Assert.AreEqual("a=b; Max-Age=0", SetCookieHeaderWriter.WriteFieldValue(new CookieDefinition("a", "b", MaxAge: 0)));
        Assert.AreEqual("a=b; Secure", SetCookieHeaderWriter.WriteFieldValue(new CookieDefinition("a", "b", Secure: true)));
        Assert.AreEqual("a=b; HttpOnly", SetCookieHeaderWriter.WriteFieldValue(new CookieDefinition("a", "b", HttpOnly: true)));
        Assert.AreEqual("a=b; SameSite=None", SetCookieHeaderWriter.WriteFieldValue(new CookieDefinition("a", "b", SameSite: "None")));
    }

    [TestMethod]
    public void WriteFieldValue_ScopingKeys_AreNeverWritten()
    {
        Assert.AreEqual("a=b", SetCookieHeaderWriter.WriteFieldValue(new CookieDefinition("a", "b", OnPath: "/p", OnListener: "http://127.0.0.1:1")));
    }

    [TestMethod]
    public void WriteFieldValue_NullCookie_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SetCookieHeaderWriter.WriteFieldValue(null!));
    }
}
