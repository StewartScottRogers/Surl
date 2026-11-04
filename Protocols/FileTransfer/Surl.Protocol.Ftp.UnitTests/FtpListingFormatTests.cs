using Surl.Content;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The line forms of <see cref="FtpListingFormat"/> (ADR-0052, decision 7).
/// </summary>
[TestClass]
public sealed class FtpListingFormatTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow(0, "Sep 29 08:00", DisplayName = "Written now: the time")]
    [DataRow(180 * 24 * 60, "Apr  2 08:00", DisplayName = "Written 180 days ago: still the time")]
    [DataRow((180 * 24 * 60) + 1, "Apr  2  2026", DisplayName = "Written 180 days and a minute ago: the year")]
    [DataRow(-1, "Sep 29  2026", DisplayName = "Written a minute from now: the year")]
    public void LongLine_File_ShowsTheTimeOnlyForAWriteWithinTheLast180Days(int minutesAgo, string date)
    {
        var entry = new ContentDirectoryEntry("a.txt", ContentEntryKind.File, 12, Now.AddMinutes(-minutesAgo));

        var line = FtpListingFormat.LongLine(entry, Now);

        Assert.AreEqual($"-rw-r--r-- 1 surl surl           12 {date} a.txt", line);
    }

    [TestMethod]
    public void LongLine_WrittenInAnotherOffset_ShowsTheUtcTime()
    {
        var entry = new ContentDirectoryEntry("d", ContentEntryKind.Directory, null, new DateTimeOffset(2026, 9, 28, 23, 30, 0, TimeSpan.FromHours(-2)));

        Assert.AreEqual("drwxr-xr-x 1 surl surl            0 Sep 29 01:30 d", FtpListingFormat.LongLine(entry, Now));
    }

    [TestMethod]
    public void LongLine_LargeFile_RightAlignsTheSizeAndWidensPastTwelveColumns()
    {
        var entry = new ContentDirectoryEntry("big", ContentEntryKind.File, 1234567890123, Now);

        Assert.AreEqual("-rw-r--r-- 1 surl surl 1234567890123 Sep 29 08:00 big", FtpListingFormat.LongLine(entry, Now));
    }

    [TestMethod]
    public void Encode_NoEntries_IsNoBytes()
    {
        Assert.IsEmpty(FtpListingFormat.Encode([], FtpListingFormat.NameLine, Now));
    }
}
