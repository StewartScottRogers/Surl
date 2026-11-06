namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>The headers exchange's match (section 3.1.3; ADR-0112 decision 5).</summary>
[TestClass]
public sealed class Amqp0HeadersMatcherTests
{
    private static readonly Amqp0Field MatchAll = new("x-match", Amqp0FieldValue.LongString("all"));
    private static readonly Amqp0Field MatchAny = new("x-match", Amqp0FieldValue.LongString("any"));
    private static readonly Amqp0Field FormatPdf = new("format", Amqp0FieldValue.LongString("pdf"));
    private static readonly Amqp0Field TypeReport = new("type", Amqp0FieldValue.LongString("report"));
    private static readonly Amqp0Field FormatZip = new("format", Amqp0FieldValue.LongString("zip"));

    [TestMethod]
    public void Matches_XMatchAll_NeedsEveryEntry()
    {
        var binding = Table(MatchAll, FormatPdf, TypeReport);

        Assert.IsTrue(Amqp0HeadersMatcher.Matches(binding, Table(TypeReport, FormatPdf, new Amqp0Field("other", Amqp0FieldValue.Int32(1)))));
        Assert.IsFalse(Amqp0HeadersMatcher.Matches(binding, Table(FormatPdf)));
        Assert.IsFalse(Amqp0HeadersMatcher.Matches(binding, null));
    }

    [TestMethod]
    public void Matches_NoXMatch_IsAll()
    {
        var binding = Table(FormatPdf, TypeReport);

        Assert.IsTrue(Amqp0HeadersMatcher.Matches(binding, Table(FormatPdf, TypeReport)));
        Assert.IsFalse(Amqp0HeadersMatcher.Matches(binding, Table(FormatPdf)));
    }

    [TestMethod]
    public void Matches_XMatchAny_NeedsOneEntry()
    {
        var binding = Table(MatchAny, FormatPdf, TypeReport);

        Assert.IsTrue(Amqp0HeadersMatcher.Matches(binding, Table(FormatZip, TypeReport)));
        Assert.IsFalse(Amqp0HeadersMatcher.Matches(binding, Table(FormatZip)));
        Assert.IsFalse(Amqp0HeadersMatcher.Matches(binding, null));
    }

    [TestMethod]
    public void Matches_SameValueOfAnotherType_DoesNotMatch()
    {
        var binding = Table(new Amqp0Field("n", Amqp0FieldValue.Int32(1)));

        Assert.IsFalse(Amqp0HeadersMatcher.Matches(binding, Table(new Amqp0Field("n", Amqp0FieldValue.Int64(1)))));
        Assert.IsTrue(Amqp0HeadersMatcher.Matches(binding, Table(new Amqp0Field("n", Amqp0FieldValue.Int32(1)))));
    }

    [TestMethod]
    public void Matches_VoidEntry_MatchesThePresenceOfTheHeader()
    {
        var binding = Table(new Amqp0Field("format", Amqp0FieldValue.Void));

        Assert.IsTrue(Amqp0HeadersMatcher.Matches(binding, Table(FormatZip)));
        Assert.IsFalse(Amqp0HeadersMatcher.Matches(binding, Table(TypeReport)));
    }

    [TestMethod]
    public void Matches_XEntries_AreNotMatched()
    {
        var reserved = new Amqp0Field("x-format", Amqp0FieldValue.LongString("pdf"));

        Assert.IsTrue(Amqp0HeadersMatcher.Matches(Table(MatchAll, reserved), null));
        Assert.IsFalse(Amqp0HeadersMatcher.Matches(Table(MatchAny, reserved), Table(reserved)));
    }

    [TestMethod]
    public void IsValidBinding_XMatchAllAnyOrAbsent_IsValidAndAnythingElseIsNot()
    {
        Assert.IsTrue(Amqp0HeadersMatcher.IsValidBinding(Table(MatchAll)));
        Assert.IsTrue(Amqp0HeadersMatcher.IsValidBinding(Table(MatchAny)));
        Assert.IsTrue(Amqp0HeadersMatcher.IsValidBinding(Amqp0FieldTable.Empty));
        Assert.IsFalse(Amqp0HeadersMatcher.IsValidBinding(Table(new Amqp0Field("x-match", Amqp0FieldValue.LongString("some")))));
        Assert.IsFalse(Amqp0HeadersMatcher.IsValidBinding(Table(new Amqp0Field("x-match", Amqp0FieldValue.Boolean(true)))));
    }

    private static Amqp0FieldTable Table(params Amqp0Field[] fields) => new(fields);
}
