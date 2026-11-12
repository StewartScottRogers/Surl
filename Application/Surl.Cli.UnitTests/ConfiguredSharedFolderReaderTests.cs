namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredSharedFolderReader"/> reading <c>&lt;name&gt;=&lt;member&gt;,...</c> items (ADR-0101
/// decisions 3 to 5, ADR-0102 decision 1.6).
/// </summary>
[TestClass]
public sealed class ConfiguredSharedFolderReaderTests
{
    [TestMethod]
    public void Read_NameAndMembers_KeepsThemInOrder()
    {
        var failure = ConfiguredSharedFolderReader.Read("team%2Fq1=ann@example.com,@sales,bob", out var folder);

        Assert.IsNull(failure);
        Assert.AreEqual("team/q1", folder.Name);
        CollectionAssert.AreEqual(
            new[] { new ConfiguredMember("ann@example.com", false), new ConfiguredMember("sales", true), new ConfiguredMember("bob", false) },
            folder.Members.ToArray());
    }

    [TestMethod]
    [DataRow("team", "expected <name>=<member>,...", DisplayName = "No separator")]
    [DataRow("=ann", "the name is empty", DisplayName = "No name")]
    [DataRow("team=", "an empty field", DisplayName = "No member")]
    [DataRow("team=ann,@", "an empty field", DisplayName = "Empty group name")]
    [DataRow("INBOX=ann", "shared folder INBOX cannot be INBOX", DisplayName = "INBOX")]
    [DataRow("inbox=ann", "shared folder inbox cannot be INBOX", DisplayName = "INBOX in another case")]
    [DataRow("team=ann,bob,ann", "member ann is given twice", DisplayName = "Member twice")]
    [DataRow("team=@sales,@sales", "member @sales is given twice", DisplayName = "Group twice")]
    [DataRow("te%09am=ann", "a control character", DisplayName = "Control character")]
    [DataRow("team=ann;readonly", "unknown flag readonly", DisplayName = "Unknown flag")]
    public void Read_RefusedItem_GivesTheReason(string argument, string reason) =>
        Assert.AreEqual(reason, ConfiguredSharedFolderReader.Read(argument, out _));

    [TestMethod]
    public void Read_AccountAndGroupOfOneName_AreTwoMembers() =>
        Assert.IsNull(ConfiguredSharedFolderReader.Read("team=sales,@sales", out _));

    [TestMethod]
    public void Read_NamePastTheStoreBound_IsRefused() =>
        Assert.AreEqual("a folder name is longer than 1024 bytes", ConfiguredSharedFolderReader.Read(new string('x', 1025) + "=ann", out _));

    [TestMethod]
    [DataRow(1024, null, DisplayName = "At the bound")]
    [DataRow(1025, "a folder name is longer than 1024 bytes", DisplayName = "Past the bound")]
    public void CheckFolderName_CountsUtf8Bytes(int bytes, string? reason) =>
        Assert.AreEqual(reason, ConfiguredSharedFolderReader.CheckFolderName(new string('x', bytes)));

    [TestMethod]
    public void CheckFolderName_LoneSurrogate_IsNotUtf8() =>
        Assert.AreEqual("a folder name is not UTF-8", ConfiguredSharedFolderReader.CheckFolderName("\uDC00"));

    [TestMethod]
    public void SharedFolderGivenTwice_NamesTheFolder() =>
        Assert.AreEqual("shared folder team is given twice", ConfiguredSharedFolderReader.SharedFolderGivenTwice("team"));

    [TestMethod]
    public void Reading_IsNamedByTheItemShape()
    {
        Assert.AreEqual("<name>=<member>,...", ConfiguredSharedFolderReader.Reading.Type.Name);
        Assert.IsNull(ConfiguredSharedFolderReader.Reading.Read("team=ann", out _));
    }
}
