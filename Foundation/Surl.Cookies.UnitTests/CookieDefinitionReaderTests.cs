namespace Surl.Cookies;

[TestClass]
public sealed class CookieDefinitionReaderTests
{
    private static CookieDefinition Read(string spec)
    {
        Assert.IsTrue(CookieDefinitionReader.TryRead(spec, out CookieDefinition? cookie, out string problem), problem);
        Assert.IsNotNull(cookie);
        return cookie;
    }

    private static string Refused(string? spec)
    {
        Assert.IsFalse(CookieDefinitionReader.TryRead(spec, out CookieDefinition? cookie, out string problem));
        Assert.IsNull(cookie);
        Assert.AreNotEqual(string.Empty, problem);
        return problem;
    }

    [TestMethod]
    public void TryRead_EveryAttribute_RoundTripsToTheAdrBytes()
    {
        CookieDefinition cookie = Read("sid=abc123;domain=127.0.0.1; PATH=/;Expires=Wed, 09 Jun 2027 10:18:14 GMT;max-age=3600;SECURE;httponly;samesite=lax;on-path=/p;on-listener=http://127.0.0.1:80/");

        Assert.AreEqual(
            "sid=abc123; Domain=127.0.0.1; Path=/; Expires=Wed, 09 Jun 2027 10:18:14 GMT; Max-Age=3600; Secure; HttpOnly; SameSite=Lax",
            SetCookieHeaderWriter.WriteFieldValue(cookie));
        Assert.AreEqual("/p", cookie.OnPath);
        Assert.AreEqual("http://127.0.0.1:80/", cookie.OnListener);
    }

    [TestMethod]
    public void TryRead_NameAndValueOnly_HasNoAttributes()
    {
        Assert.AreEqual(new CookieDefinition("a", "b"), Read("a=b"));
    }

    [TestMethod]
    public void TryRead_EmptyValueAndValueHoldingEquals_AreKeptAsWritten()
    {
        Assert.AreEqual(string.Empty, Read("a=").Value);
        Assert.AreEqual("x=y z", Read("a=x=y z").Value);
    }

    [TestMethod]
    public void TryRead_SameSiteNoneWithoutSecure_IsKept()
    {
        Assert.AreEqual("None", Read("a=b;SameSite=NONE").SameSite);
    }

    [TestMethod]
    public void TryRead_MaxAgeZero_IsKept()
    {
        Assert.AreEqual(0L, Read("a=b;Max-Age=0").MaxAge);
    }

    [TestMethod]
    public void TryRead_NullOrNoEquals_IsRefused()
    {
        StringAssert.Contains(Refused(null), "name=value");
        StringAssert.Contains(Refused("abc"), "name=value");
    }

    [TestMethod]
    public void TryRead_BadName_IsRefused()
    {
        Refused("=v");
        Refused("a b=v");
        Refused("a,b=v");
        Refused("a\u0001=v");
        Refused(";Path=/");
    }

    [TestMethod]
    public void TryRead_ControlCharacterInValue_IsRefused()
    {
        StringAssert.Contains(Refused("a=b\r\nX: y"), "value");
    }

    [TestMethod]
    public void TryRead_UnknownAttribute_IsRefused()
    {
        StringAssert.Contains(Refused("a=b;Priority=High"), "priority");
    }

    [TestMethod]
    public void TryRead_RepeatedAttribute_IsRefused()
    {
        StringAssert.Contains(Refused("a=b;Path=/;path=/x"), "twice");
        StringAssert.Contains(Refused("a=b;Secure;Secure"), "twice");
    }

    [TestMethod]
    public void TryRead_ValuedAttributeWithoutValue_IsRefused()
    {
        StringAssert.Contains(Refused("a=b;Path"), "needs a value");
        StringAssert.Contains(Refused("a=b;Path="), "needs a value");
    }

    [TestMethod]
    public void TryRead_FlagWithValue_IsRefused()
    {
        StringAssert.Contains(Refused("a=b;Secure=1"), "takes no value");
        StringAssert.Contains(Refused("a=b;HttpOnly=true"), "takes no value");
    }

    [TestMethod]
    public void TryRead_ControlCharacterInAttributeValue_IsRefused()
    {
        StringAssert.Contains(Refused("a=b;Path=/\u0001"), "control");
    }

    [TestMethod]
    public void TryRead_BadExpires_IsRefused()
    {
        StringAssert.Contains(Refused("a=b;Expires=tomorrow"), "IMF-fixdate");
    }

    [TestMethod]
    public void TryRead_NegativeOrNonNumericMaxAge_IsRefused()
    {
        StringAssert.Contains(Refused("a=b;Max-Age=-1"), "max-age");
        StringAssert.Contains(Refused("a=b;Max-Age=soon"), "max-age");
    }

    [TestMethod]
    public void TryRead_BadSameSite_IsRefused()
    {
        StringAssert.Contains(Refused("a=b;SameSite=Sideways"), "samesite");
    }
}
