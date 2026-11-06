namespace Surl.Control;

[TestClass]
public sealed class ControlTokenTests
{
    private const string Token = ControlHarness.Token;

    [TestMethod]
    public void Constructor_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new ControlToken(null!));

    [TestMethod]
    [DataRow(31, 'a')]
    [DataRow(1025, 'a')]
    [DataRow(32, ' ')]
    [DataRow(32, '\u007f')]
    [DataRow(32, 'é')]
    public void Constructor_WrongLengthOrNotVisibleAscii_Throws(int length, char character)
    {
        var token = new string(character, length);

        var failure = Assert.ThrowsExactly<ArgumentException>(() => new ControlToken(token));

        StringAssert.StartsWith(failure.Message, "A control token is 32 to 1024 visible ASCII bytes.");
    }

    [TestMethod]
    [DataRow(32)]
    [DataRow(1024)]
    public void Constructor_LengthAtItsBounds_IsAccepted(int length)
    {
        var token = new ControlToken(new string('~', length));

        Assert.IsTrue(token.IsPresentedIn([new("Authorization", $"Bearer {new string('~', length)}")]));
    }

    [TestMethod]
    [DataRow("Authorization", "Bearer " + Token, true)]
    [DataRow("authorization", "bearer  " + Token + " ", true)]
    [DataRow("Authorization", "Bearer " + Token + "x", false)]
    [DataRow("Authorization", "Basic " + Token, false)]
    [DataRow("Authorization", "Bearer", false)]
    [DataRow("X-Token", "Bearer " + Token, false)]
    public void IsPresentedIn_OneField_IsTrueOnlyForTheBearerToken(string name, string value, bool expected)
    {
        var token = new ControlToken(Token);

        var isPresented = token.IsPresentedIn([new(name, value)]);

        Assert.AreEqual(expected, isPresented);
    }

    [TestMethod]
    public void IsPresentedIn_TwoAuthorizationFields_IsFalse()
    {
        var token = new ControlToken(Token);

        var isPresented = token.IsPresentedIn([new("Authorization", $"Bearer {Token}"), new("Authorization", $"Bearer {Token}")]);

        Assert.IsFalse(isPresented);
    }

    [TestMethod]
    public void IsPresentedIn_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new ControlToken(Token).IsPresentedIn(null!));
}
