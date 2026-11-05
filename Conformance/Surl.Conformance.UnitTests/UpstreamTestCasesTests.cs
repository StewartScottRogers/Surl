using System.Security.Cryptography;

namespace Surl.Conformance;

/// <summary>
/// Checks that the imported copy of upstream curl's test cases (ADR-0080 decision 1) reaches the
/// test output with the bytes of tag curl-8_21_0, as <c>SHA256SUMS</c> records them. Reading the
/// files beside the test assembly is deliberate and fast: a line-ending normalisation anywhere
/// must fail the default run on every platform.
/// </summary>
[TestClass]
public sealed class UpstreamTestCasesTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "UpstreamTestCases");

    [TestMethod]
    public void Sha256Sums_EveryListedFile_HasTheListedHash()
    {
        var mismatches = new List<string>();

        foreach (var (hash, path) in ReadSha256Sums())
        {
            var actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root, path))));
            if (actual != hash)
            {
                mismatches.Add(path);
            }
        }

        Assert.IsEmpty(mismatches, string.Join(", ", mismatches));
    }

    [TestMethod]
    public void Sha256Sums_ListsEveryFileInTheOutputAndNoOther()
    {
        var listed = ReadSha256Sums().Select(entry => entry.Path).Order(StringComparer.Ordinal).ToArray();

        var present = Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(Root, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(path => path != "SHA256SUMS")
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(listed, present);
    }

    [TestMethod]
    public void Data_HoldsTheTagsTestCases_All2013()
    {
        var cases = ReadSha256Sums().Count(entry => IsTestCase(entry.Path));

        Assert.AreEqual(2013, cases);
    }

    [TestMethod]
    public void Data_HoldsDisabledAndCopyingBesideIt()
    {
        var listed = ReadSha256Sums().Select(entry => entry.Path).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("data/DISABLED", listed);
        Assert.Contains("COPYING", listed);
    }

    private static bool IsTestCase(string path) =>
        path.StartsWith("data/test", StringComparison.Ordinal) && path["data/test".Length..].All(char.IsAsciiDigit);

    private static List<(string Hash, string Path)> ReadSha256Sums() =>
        File.ReadAllLines(Path.Combine(Root, "SHA256SUMS"))
            .Where(line => line.Length > 0)
            .Select(line => (line[..64], line[66..]))
            .ToList();
}
