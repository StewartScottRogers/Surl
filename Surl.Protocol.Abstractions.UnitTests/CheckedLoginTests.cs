namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class CheckedLoginTests
{
    [TestMethod]
    [DataRow("Basic", "alice", true, "Login accepted: Basic alice")]
    [DataRow("Digest", "alice", false, "Login refused: Digest alice")]
    [DataRow("Bearer", CheckedLogin.BearerTokenUser, true, "Login accepted: Bearer bearer token")]
    [DataRow("mqtt", "tester", false, "Login refused: mqtt tester")]
    public void Note_WithAUser_NamesTheAnswerTheMethodAndTheUser(string method, string user, bool isAccepted, string note)
    {
        var checkedLogin = new CheckedLogin(method, user, isAccepted);

        Assert.AreEqual(note, checkedLogin.Note);
    }

    [TestMethod]
    public void Note_NoUserReadable_LeavesTheUserOut()
    {
        var checkedLogin = new CheckedLogin("Basic", null, false);

        Assert.AreEqual("Login refused: Basic", checkedLogin.Note);
    }

    [TestMethod]
    public void HttpAuthenticationVerdict_ConstructedWithoutACheckedLogin_HasNone()
    {
        var verdict = new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null);

        Assert.IsNull(verdict.CheckedLogin);
    }

    [TestMethod]
    public void HttpAuthenticationVerdict_ConstructedWithACheckedLogin_KeepsIt()
    {
        var checkedLogin = new CheckedLogin("Basic", "alice", true);

        var verdict = new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "alice", checkedLogin);

        Assert.AreSame(checkedLogin, verdict.CheckedLogin);
    }
}
