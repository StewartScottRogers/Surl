namespace Surl.Console;

[TestClass]
public sealed class LogFileTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Open_Append_KeepsWhatTheFileHeldAndWritesEachLineAtOnce()
    {
        var path = Path.Combine(Path.GetTempPath(), "surl-log-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            File.WriteAllText(path, "earlier run" + Environment.NewLine);

            using (var writer = LogFile.Open(path, FileMode.Append))
            {
                writer.WriteLine("this run");

                Assert.AreEqual("earlier run" + Environment.NewLine + "this run" + Environment.NewLine, ReadShared(path));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Open_Create_TruncatesTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "surl-trace-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(path, new string('x', 5000));

            using (var writer = LogFile.Open(path, FileMode.Create))
            {
                writer.Write("dump");
            }

            Assert.AreEqual("dump", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Open_MissingDirectory_ThrowsDirectoryNotFoundException()
    {
        var path = Path.Combine(Path.GetTempPath(), "surl-missing-" + Guid.NewGuid().ToString("N"), "surl.log");

        Assert.ThrowsExactly<DirectoryNotFoundException>(() => LogFile.Open(path, FileMode.Append));
    }

    private static string ReadShared(string path)
    {
        using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        return reader.ReadToEnd();
    }
}
