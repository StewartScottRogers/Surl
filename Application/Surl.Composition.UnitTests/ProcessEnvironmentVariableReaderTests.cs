namespace Surl.Composition;

[TestClass]
public sealed class ProcessEnvironmentVariableReaderTests
{
    [TestMethod]
    public void Read_VariableTheProcessHas_ReturnsItsValue()
    {
        var reader = new ProcessEnvironmentVariableReader();

        var value = reader.Read("PATH");

        Assert.AreEqual(Environment.GetEnvironmentVariable("PATH"), value);
    }

    [TestMethod]
    public void Read_VariableNotSet_ReturnsNull()
    {
        var reader = new ProcessEnvironmentVariableReader();

        var value = reader.Read("SURL_TEST_" + Guid.NewGuid().ToString("N"));

        Assert.IsNull(value);
    }
}
