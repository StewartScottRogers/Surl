namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The distribution specification's repository name and tag grammars (Dist "Pulling manifests").
/// </summary>
[TestClass]
public sealed class OciNamesTests
{
    [TestMethod]
    [DataRow("a")]
    [DataRow("library/alpine")]
    [DataRow("a.b_c__d-e---f9")]
    [DataRow("x/y/z0")]
    public void IsRepositoryName_OnTheGrammar_IsTrue(string name)
    {
        Assert.IsTrue(OciNames.IsRepositoryName(name));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("A")]
    [DataRow("a/")]
    [DataRow("/a")]
    [DataRow("a//b")]
    [DataRow(".a")]
    [DataRow("a.")]
    [DataRow("a..b")]
    [DataRow("a___b")]
    [DataRow("a._b")]
    [DataRow("a-.b")]
    [DataRow("a:b")]
    [DataRow("a b")]
    public void IsRepositoryName_OffTheGrammar_IsFalse(string name)
    {
        Assert.IsFalse(OciNames.IsRepositoryName(name));
    }

    [TestMethod]
    public void IsRepositoryName_255And256Bytes_IsTrueThenFalse()
    {
        Assert.IsTrue(OciNames.IsRepositoryName(new string('a', 255)));
        Assert.IsFalse(OciNames.IsRepositoryName(new string('a', 256)));
    }

    [TestMethod]
    [DataRow("latest")]
    [DataRow("_v1.2-RC")]
    [DataRow("9")]
    public void IsTag_OnTheGrammar_IsTrue(string tag)
    {
        Assert.IsTrue(OciNames.IsTag(tag));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(".v1")]
    [DataRow("-v1")]
    [DataRow("v 1")]
    [DataRow("v/1")]
    public void IsTag_OffTheGrammar_IsFalse(string tag)
    {
        Assert.IsFalse(OciNames.IsTag(tag));
    }

    [TestMethod]
    public void IsTag_128And129Characters_IsTrueThenFalse()
    {
        Assert.IsTrue(OciNames.IsTag(new string('a', 128)));
        Assert.IsFalse(OciNames.IsTag(new string('a', 129)));
    }
}
