namespace Surl.Conformance;

/// <summary>
/// Reads the imported <c>COPYING</c> beside the test assembly, as <see cref="UpstreamTestCasesTests"/>
/// reads the imported data, so the default run covers the reader on every platform.
/// </summary>
[TestClass]
public sealed class FileSystemUpstreamTestCaseFileReaderTests
{
    [TestMethod]
    public void ReadAllBytes_ImportedFile_IsItsBytes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "UpstreamTestCases", "COPYING");

        var bytes = new FileSystemUpstreamTestCaseFileReader().ReadAllBytes(path);

        CollectionAssert.AreEqual(File.ReadAllBytes(path), bytes);
    }
}
