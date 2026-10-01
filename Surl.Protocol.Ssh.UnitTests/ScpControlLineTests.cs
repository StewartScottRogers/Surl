using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// SCP's control lines as ADR-0054 decisions 3 and 4 write and read them.
/// </summary>
[TestClass]
public sealed class ScpControlLineTests
{
    [TestMethod]
    public void Times_WritesTheModificationTimeTwiceInWholeSeconds()
    {
        var line = ScpControlLine.Times(new DateTimeOffset(2026, 9, 27, 12, 34, 56, 789, TimeSpan.Zero));

        Assert.AreEqual("T1790512496 0 1790512496 0\n", Encoding.ASCII.GetString(line));
    }

    [TestMethod]
    public void Times_BeforeTheEpoch_IsZero()
    {
        var line = ScpControlLine.Times(new DateTimeOffset(1960, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.AreEqual("T0 0 0 0\n", Encoding.ASCII.GetString(line));
    }

    [TestMethod]
    public void File_WritesMode0644TheSizeAndTheNameInUtf8()
    {
        Assert.AreEqual("C0644 1234 café.txt\n", Encoding.UTF8.GetString(ScpControlLine.File(1234, "café.txt")));
    }

    [TestMethod]
    [DataRow("T1790512496 0 1790512400 0", 1790512496L)]
    [DataRow("T0 999999 0 999999", 0L)]
    [DataRow("T253402300799 0 0 0", 253402300799L)]
    public void TryReadTimes_WellFormedLine_ReadsTheModificationTime(string line, long seconds)
    {
        Assert.IsTrue(ScpControlLine.TryReadTimes(Encoding.ASCII.GetBytes(line), out var lastWriteTime));
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(seconds), lastWriteTime);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("C0644 5 a")]
    [DataRow("T1 0 1")]
    [DataRow("T1 0 1 0 0")]
    [DataRow("T1 0 1 x")]
    [DataRow("T1  0 1 0")]
    [DataRow("T-1 0 1 0")]
    [DataRow("T253402300800 0 0 0")]
    [DataRow("T99999999999999999999 0 0 0")]
    public void TryReadTimes_AnythingElse_IsNotATLine(string line)
    {
        Assert.IsFalse(ScpControlLine.TryReadTimes(Encoding.ASCII.GetBytes(line), out var lastWriteTime));
        Assert.AreEqual(default, lastWriteTime);
    }

    [TestMethod]
    [DataRow("C0644 5 a.txt", "0644", 5L, "a.txt")]
    [DataRow("C7 0 b", "7", 0L, "b")]
    [DataRow("C007777 9223372036854775807 name with spaces", "007777", long.MaxValue, "name with spaces")]
    [DataRow("C0644 5 café", "0644", 5L, "café")]
    public void ReadFile_WellFormedCLine_ReadsTheHeader(string line, string mode, long size, string name)
    {
        var error = ScpControlLine.ReadFile(Encoding.UTF8.GetBytes(line), out var header);

        Assert.AreEqual(string.Empty, error);
        Assert.AreEqual(new ScpFileHeader(mode, size, name), header);
    }

    [TestMethod]
    [DataRow("", "unexpected line")]
    [DataRow("X", "unexpected line")]
    [DataRow("D0755 0 sub", "received directory without -r")]
    [DataRow("E", "received directory without -r")]
    [DataRow("C", "bad mode")]
    [DataRow("C 5 a", "bad mode")]
    [DataRow("C0648 5 a", "bad mode")]
    [DataRow("C0000644 5 a", "bad mode")]
    [DataRow("C10000 5 a", "bad mode")]
    [DataRow("C-644 5 a", "bad mode")]
    [DataRow("C0644", "bad size")]
    [DataRow("C0644 ", "bad size")]
    [DataRow("C0644 +5 a", "bad size")]
    [DataRow("C0644 -5 a", "bad size")]
    [DataRow("C0644 9223372036854775808 a", "bad size")]
    [DataRow("C0644 12345678901234567890 a", "bad size")]
    [DataRow("C0644 5", "unexpected filename")]
    [DataRow("C0644 5 ", "unexpected filename")]
    [DataRow("C0644 5 .", "unexpected filename")]
    [DataRow("C0644 5 ..", "unexpected filename")]
    [DataRow("C0644 5 a/b", "unexpected filename")]
    public void ReadFile_AnythingElse_SaysWhatIsWrong(string line, string expected)
    {
        var error = ScpControlLine.ReadFile(Encoding.UTF8.GetBytes(line), out var header);

        Assert.AreEqual(expected, error);
        Assert.IsNull(header);
    }

    [TestMethod]
    public void ReadFile_NameNotUtf8_IsUnexpectedFilename()
    {
        var error = ScpControlLine.ReadFile([.. Encoding.ASCII.GetBytes("C0644 5 a"), 0xFF], out var header);

        Assert.AreEqual("unexpected filename", error);
        Assert.IsNull(header);
    }
}
