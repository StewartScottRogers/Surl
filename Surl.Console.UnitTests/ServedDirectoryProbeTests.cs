namespace Surl.Console;

[TestClass]
public sealed class ServedDirectoryProbeTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public void CanOpen_ExistingDirectory_ReturnsTrue()
    {
        var directory = Directory.CreateTempSubdirectory("surl-probe-");
        try
        {
            var canOpen = ServedDirectoryProbe.CanOpen(directory.FullName);

            Assert.IsTrue(canOpen);
        }
        finally
        {
            directory.Delete();
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void CanOpen_MissingDirectory_ReturnsFalse()
    {
        var missing = Path.Combine(Path.GetTempPath(), "surl-missing-" + Guid.NewGuid().ToString("N"));

        var canOpen = ServedDirectoryProbe.CanOpen(missing);

        Assert.IsFalse(canOpen);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void CanOpen_File_ReturnsFalse()
    {
        var file = Path.GetTempFileName();
        try
        {
            var canOpen = ServedDirectoryProbe.CanOpen(file);

            Assert.IsFalse(canOpen);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
