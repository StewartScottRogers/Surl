namespace Surl.Extended.Protocol.S3;

/// <summary>
/// GetObject's conditional fields and their precedence, from the GetObject page.
/// </summary>
[TestClass]
public sealed class S3ConditionsTests
{
    private const string ETag = "\"abc\"";
    private const string Before = "Wed, 04 Nov 2026 12:00:00 GMT";
    private const string At = "Thu, 05 Nov 2026 12:00:00 GMT";
    private static readonly DateTimeOffset LastModified = new(2026, 11, 5, 12, 0, 0, 500, TimeSpan.Zero);

    [TestMethod]
    [DataRow(new string[0], "Serve", DisplayName = "no condition")]
    [DataRow(new[] { "If-Match", "\"abc\"" }, "Serve", DisplayName = "If-Match matching")]
    [DataRow(new[] { "if-match", "abc" }, "Serve", DisplayName = "If-Match unquoted")]
    [DataRow(new[] { "If-Match", "\"x\", *" }, "Serve", DisplayName = "If-Match any")]
    [DataRow(new[] { "If-Match", "\"x\"" }, "PreconditionFailed", DisplayName = "If-Match failing")]
    [DataRow(new[] { "If-Unmodified-Since", At }, "Serve", DisplayName = "If-Unmodified-Since holding")]
    [DataRow(new[] { "If-Unmodified-Since", Before }, "PreconditionFailed", DisplayName = "If-Unmodified-Since failing")]
    [DataRow(new[] { "If-Unmodified-Since", "yesterday" }, "Serve", DisplayName = "If-Unmodified-Since not a date")]
    [DataRow(new[] { "If-Match", "\"abc\"", "If-Unmodified-Since", Before }, "Serve", DisplayName = "If-Match true over If-Unmodified-Since false")]
    [DataRow(new[] { "If-None-Match", "\"abc\"" }, "NotModified", DisplayName = "If-None-Match matching")]
    [DataRow(new[] { "If-None-Match", "\"x\"" }, "Serve", DisplayName = "If-None-Match not matching")]
    [DataRow(new[] { "If-Modified-Since", At }, "NotModified", DisplayName = "If-Modified-Since unmodified")]
    [DataRow(new[] { "If-Modified-Since", Before }, "Serve", DisplayName = "If-Modified-Since modified")]
    [DataRow(new[] { "If-None-Match", "\"abc\"", "If-Modified-Since", Before }, "NotModified", DisplayName = "If-None-Match false over If-Modified-Since true")]
    [DataRow(new[] { "If-Match", "\"x\"", "If-None-Match", "\"abc\"" }, "PreconditionFailed", DisplayName = "412 over 304")]
    public void Evaluate_Fields_DecideAsThePageSays(string[] namesAndValues, string expected)
    {
        var fields = namesAndValues.Chunk(2).Select(pair => new KeyValuePair<string, string>(pair[0], pair[1])).ToArray();

        var outcome = S3Conditions.Evaluate(fields, ETag, LastModified);

        Assert.AreEqual(expected, outcome.ToString());
    }
}
