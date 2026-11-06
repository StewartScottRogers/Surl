namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>Section 3.1.3.3's topic matching: <c>*</c> one word, <c>#</c> zero or more.</summary>
[TestClass]
public sealed class Amqp0TopicKeyMatcherTests
{
    [TestMethod]
    [DataRow("a.b.c", "a.b.c", true)]
    [DataRow("a.*.c", "a.b.c", true)]
    [DataRow("a.*.c", "a.c", false)]
    [DataRow("a.#", "a", true)]
    [DataRow("a.#", "a.b.c", true)]
    [DataRow("#.c", "a.b.c", true)]
    [DataRow("#", "", true)]
    [DataRow("a.#.#.d", "a.b.c.d", true)]
    [DataRow("a.b", "a.b.c", false)]
    [DataRow("a.b.c", "a.b", false)]
    [DataRow("a.x", "a.b", false)]
    public void Matches_BindingKeyAndRoutingKey_IsAsSection3133Says(string bindingKey, string routingKey, bool matches) =>
        Assert.AreEqual(matches, Amqp0TopicKeyMatcher.Matches(bindingKey, routingKey));

    [TestMethod]
    public void Matches_ManyHashWords_EndsAtOnce()
    {
        var bindingKey = string.Join('.', Enumerable.Repeat("#", 100)) + ".z";
        var routingKey = string.Join('.', Enumerable.Repeat("a", 100));

        Assert.IsFalse(Amqp0TopicKeyMatcher.Matches(bindingKey, routingKey));
    }
}
