namespace Surl.Console;

[TestClass]
public sealed class DiskConfigFileSourceTests
{
    [TestMethod]
    public void Read_StandardInputLongerThanTheLimit_StopsAtTheLimit()
    {
        var source = new DiskConfigFileSource(() => new MemoryStream(new byte[200000]), fileNamesIgnoreCase: false);

        var content = source.Read("-", 100001);

        Assert.IsNotNull(content);
        Assert.HasCount(100001, content);
    }

    [TestMethod]
    public void Read_StandardInputShorterThanTheLimit_ReadsItAll()
    {
        var source = new DiskConfigFileSource(() => new MemoryStream("url = x\n"u8.ToArray()), fileNamesIgnoreCase: false);

        var content = source.Read("-", 1000);

        CollectionAssert.AreEqual("url = x\n"u8.ToArray(), content);
    }

    [TestMethod]
    public void Read_StandardInputThatFails_IsNull()
    {
        var source = new DiskConfigFileSource(() => throw new IOException("closed"), fileNamesIgnoreCase: false);

        Assert.IsNull(source.Read("-", 1000));
    }

    [TestMethod]
    public void Read_PathWithAnInvalidCharacter_IsNull()
    {
        var source = new DiskConfigFileSource(() => Stream.Null, fileNamesIgnoreCase: false);

        Assert.IsNull(source.Read("a\0b.cfg", 1000));
    }

    [TestMethod]
    public void IdentityOf_TheSameFileWrittenTwoWays_IsTheSame()
    {
        var source = new DiskConfigFileSource(() => Stream.Null, fileNamesIgnoreCase: false);

        Assert.AreEqual(source.IdentityOf("a.cfg"), source.IdentityOf(Path.Combine(".", "a.cfg")));
    }

    [TestMethod]
    public void IdentityOf_NamesCompared_IsTheFullPathAsWritten()
    {
        var source = new DiskConfigFileSource(() => Stream.Null, fileNamesIgnoreCase: false);

        Assert.AreEqual(Path.GetFullPath("a.cfg"), source.IdentityOf("a.cfg"));
        Assert.AreNotEqual(source.IdentityOf("a.cfg"), source.IdentityOf("A.CFG"));
    }

    [TestMethod]
    public void IdentityOf_NamesIgnoringCase_IsTheSameForEitherCase()
    {
        var source = new DiskConfigFileSource(() => Stream.Null, fileNamesIgnoreCase: true);

        Assert.AreEqual(source.IdentityOf("a.cfg"), source.IdentityOf("A.CFG"));
    }
}
