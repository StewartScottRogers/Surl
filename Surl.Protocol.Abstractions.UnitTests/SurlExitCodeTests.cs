namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class SurlExitCodeTests
{
    [TestMethod]
    [DataRow(SurlExitCode.Ok, 0)]
    [DataRow(SurlExitCode.UnsupportedProtocol, 1)]
    [DataRow(SurlExitCode.FailedInit, 2)]
    [DataRow(SurlExitCode.MalformedUrl, 3)]
    [DataRow(SurlExitCode.CouldNotResolveHost, 6)]
    [DataRow(SurlExitCode.CouldNotReadFile, 37)]
    [DataRow(SurlExitCode.BindFailed, 45)]
    [DataRow(SurlExitCode.InternalError, 125)]
    public void Value_EachMember_HasTheNumberTheAdrAssigns(SurlExitCode member, int expectedNumber)
    {
        Assert.AreEqual(expectedNumber, (int)member);
    }

    [TestMethod]
    public void GetValues_Always_HasOneMemberPerAdrRow()
    {
        Assert.HasCount(8, Enum.GetValues<SurlExitCode>());
    }
}
