namespace Surl.Kerberos;

/// <summary>
/// Pins <see cref="KerberosPrincipalName" />'s RFC 1964 section 2.1.1 display form (ADR-0057
/// decision 10) and its ordinal equality.
/// </summary>
[TestClass]
public sealed class KerberosPrincipalNameTests
{
    [TestMethod]
    [DataRow("EXAMPLE.COM", new[] { "user" }, "user@EXAMPLE.COM", DisplayName = "one component")]
    [DataRow("EXAMPLE.COM", new[] { "host", "web01" }, "host/web01@EXAMPLE.COM", DisplayName = "two components")]
    [DataRow("EXAMPLE.COM", new[] { "a/b" }, "a\\/b@EXAMPLE.COM", DisplayName = "slash in a component")]
    [DataRow("EXAMPLE.COM", new[] { "a@b" }, "a\\@b@EXAMPLE.COM", DisplayName = "at sign in a component")]
    [DataRow("EXAMPLE.COM", new[] { "a\\b" }, "a\\\\b@EXAMPLE.COM", DisplayName = "backslash in a component")]
    [DataRow("EX@AMPLE/\\", new[] { "u" }, "u@EX\\@AMPLE\\/\\\\", DisplayName = "all three in the realm")]
    public void ToString_Name_IsTheRfc1964DisplayForm(string realm, string[] components, string expected)
    {
        KerberosPrincipalName name = new(realm, components);

        string text = name.ToString();

        Assert.AreEqual(expected, text);
    }

    [TestMethod]
    public void Equals_SameRealmAndComponentsInOtherLists_IsTrueWithTheSameHashCode()
    {
        KerberosPrincipalName first = new("EXAMPLE.COM", ["host", "web01"]);
        KerberosPrincipalName second = new("EXAMPLE.COM", new List<string> { "host", "web01" });

        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    [DataRow("example.com", new[] { "host", "web01" }, DisplayName = "realm in another case")]
    [DataRow("EXAMPLE.COM", new[] { "HOST", "web01" }, DisplayName = "component in another case")]
    [DataRow("EXAMPLE.COM", new[] { "host" }, DisplayName = "fewer components")]
    public void Equals_OtherName_IsFalse(string realm, string[] components)
    {
        KerberosPrincipalName first = new("EXAMPLE.COM", ["host", "web01"]);

        Assert.AreNotEqual(first, new KerberosPrincipalName(realm, components));
    }

    [TestMethod]
    public void Equals_Null_IsFalse()
    {
        KerberosPrincipalName name = new("EXAMPLE.COM", ["user"]);

        Assert.IsFalse(name.Equals(null));
    }
}
