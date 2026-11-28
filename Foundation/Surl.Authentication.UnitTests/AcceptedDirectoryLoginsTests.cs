namespace Surl.Authentication;

/// <summary>
/// Which name <see cref="AcceptedDirectoryLogins"/> drops once it holds
/// <see cref="AcceptedDirectoryLogins.MaxNames"/> names (ADR-0167, Amendment 3, point 6).
/// </summary>
[TestClass]
public sealed class AcceptedDirectoryLoginsTests
{
    private readonly ManualTimeProvider clock = new();

    private void RecordUsers(AcceptedDirectoryLogins logins, int count)
    {
        for (var user = 0; user < count; user++)
        {
            logins.Record($"user{user}");
            clock.Advance(TimeSpan.FromMilliseconds(1));
        }
    }

    [TestMethod]
    public void Record_ANewNameWhenFull_DropsOnlyTheNameWithTheOldestLogin()
    {
        var logins = new AcceptedDirectoryLogins(clock);
        RecordUsers(logins, AcceptedDirectoryLogins.MaxNames);

        logins.Record("new");

        Assert.IsFalse(logins.IsRecentlyAccepted("user0"));
        Assert.IsTrue(logins.IsRecentlyAccepted("user1"));
        Assert.IsTrue(logins.IsRecentlyAccepted("new"));
    }

    [TestMethod]
    public void Record_ANameLoggedInAgainWhenFull_DropsNothingAndMakesAnotherNameTheOldest()
    {
        var logins = new AcceptedDirectoryLogins(clock);
        RecordUsers(logins, AcceptedDirectoryLogins.MaxNames);

        logins.Record("user0");
        Assert.IsTrue(logins.IsRecentlyAccepted("user1"));
        logins.Record("new");

        Assert.IsTrue(logins.IsRecentlyAccepted("user0"));
        Assert.IsFalse(logins.IsRecentlyAccepted("user1"));
    }

    [TestMethod]
    public void Record_ANewNameWhenFullOfSomeOutdatedNames_DropsThoseAndKeepsEveryRecentOne()
    {
        var logins = new AcceptedDirectoryLogins(clock);
        logins.Record("outdated");
        clock.Advance(AcceptedDirectoryLogins.HonouredFor);
        RecordUsers(logins, AcceptedDirectoryLogins.MaxNames - 1);

        logins.Record("new");

        Assert.IsFalse(logins.IsRecentlyAccepted("outdated"));
        Assert.IsTrue(logins.IsRecentlyAccepted("user0"));
        Assert.IsTrue(logins.IsRecentlyAccepted("new"));
    }
}
