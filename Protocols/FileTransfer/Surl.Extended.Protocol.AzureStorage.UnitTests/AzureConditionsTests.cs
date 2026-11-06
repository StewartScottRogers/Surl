using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Get Blob's conditional fields and their precedence, from "Specifying conditional headers for Blob service operations".
/// </summary>
[TestClass]
public sealed class AzureConditionsTests
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
    [DataRow(new[] { "If-Match", "\"x\"" }, "ConditionNotMet", DisplayName = "If-Match failing")]
    [DataRow(new[] { "If-Unmodified-Since", At }, "Serve", DisplayName = "If-Unmodified-Since holding")]
    [DataRow(new[] { "If-Unmodified-Since", Before }, "ConditionNotMet", DisplayName = "If-Unmodified-Since failing")]
    [DataRow(new[] { "If-Unmodified-Since", "yesterday" }, "Serve", DisplayName = "If-Unmodified-Since not a date")]
    [DataRow(new[] { "If-Match", "\"abc\"", "If-Unmodified-Since", Before }, "Serve", DisplayName = "If-Match true over If-Unmodified-Since false")]
    [DataRow(new[] { "If-None-Match", "\"abc\"" }, "NotModified", DisplayName = "If-None-Match matching")]
    [DataRow(new[] { "If-None-Match", "\"x\"" }, "Serve", DisplayName = "If-None-Match not matching")]
    [DataRow(new[] { "If-Modified-Since", At }, "NotModified", DisplayName = "If-Modified-Since unmodified")]
    [DataRow(new[] { "If-Modified-Since", Before }, "Serve", DisplayName = "If-Modified-Since modified")]
    [DataRow(new[] { "If-None-Match", "\"abc\"", "If-Modified-Since", Before }, "NotModified", DisplayName = "If-None-Match false over If-Modified-Since true")]
    [DataRow(new[] { "If-Match", "\"x\"", "If-None-Match", "\"abc\"" }, "ConditionNotMet", DisplayName = "412 over 304")]
    public void Evaluate_Fields_DecideAsThePageSays(string[] namesAndValues, string expected)
    {
        var fields = namesAndValues.Chunk(2).Select(pair => new KeyValuePair<string, string>(pair[0], pair[1])).ToArray();

        var outcome = AzureConditions.Evaluate(fields, ETag, LastModified);

        Assert.AreEqual(expected, outcome.ToString());
    }

    [TestMethod]
    [DataRow(new string[0], "none", DisplayName = "no condition")]
    [DataRow(new[] { "If-Match", "\"abc\"" }, "none", DisplayName = "If-Match matching")]
    [DataRow(new[] { "If-Match", "*" }, "none", DisplayName = "If-Match any")]
    [DataRow(new[] { "If-Match", "\"x\"" }, "ConditionNotMet", DisplayName = "If-Match failing")]
    [DataRow(new[] { "If-Unmodified-Since", At }, "none", DisplayName = "If-Unmodified-Since holding")]
    [DataRow(new[] { "If-Unmodified-Since", Before }, "ConditionNotMet", DisplayName = "If-Unmodified-Since failing")]
    [DataRow(new[] { "If-Unmodified-Since", "yesterday" }, "none", DisplayName = "If-Unmodified-Since not a date")]
    [DataRow(new[] { "If-Match", "\"abc\"", "If-Unmodified-Since", Before }, "ConditionNotMet", DisplayName = "every condition counts on a write")]
    [DataRow(new[] { "If-None-Match", "\"abc\"" }, "ConditionNotMet", DisplayName = "If-None-Match matching")]
    [DataRow(new[] { "If-None-Match", "\"x\"" }, "none", DisplayName = "If-None-Match not matching")]
    [DataRow(new[] { "If-None-Match", "*" }, "BlobAlreadyExists", DisplayName = "If-None-Match any")]
    [DataRow(new[] { "If-None-Match", "\"x\", *" }, "BlobAlreadyExists", DisplayName = "If-None-Match listing any")]
    [DataRow(new[] { "If-Modified-Since", At }, "ConditionNotMet", DisplayName = "If-Modified-Since unmodified")]
    [DataRow(new[] { "If-Modified-Since", Before }, "none", DisplayName = "If-Modified-Since modified")]
    [DataRow(new[] { "If-Modified-Since", "yesterday" }, "none", DisplayName = "If-Modified-Since not a date")]
    public void WriteRefusal_FieldsOnAnExistingBlob_DecideAsTheAdrSays(string[] namesAndValues, string expected)
    {
        var fields = namesAndValues.Chunk(2).Select(pair => new KeyValuePair<string, string>(pair[0], pair[1])).ToArray();
        var existing = new ObjectProperties("text/plain", null, null, null, null, [], ETag, 0, 11, LastModified);

        var refusal = AzureConditions.WriteRefusal(fields, existing);

        Assert.AreEqual(expected, refusal?.Code ?? "none");
    }

    [TestMethod]
    [DataRow(new string[0], "none", DisplayName = "no condition")]
    [DataRow(new[] { "If-Match", "*" }, "ConditionNotMet", DisplayName = "If-Match, with no tag to match")]
    [DataRow(new[] { "If-None-Match", "*" }, "none", DisplayName = "If-None-Match any")]
    [DataRow(new[] { "If-Unmodified-Since", Before, "If-Modified-Since", At }, "none", DisplayName = "dates")]
    public void WriteRefusal_FieldsWithNoBlob_FailOnlyIfMatch(string[] namesAndValues, string expected)
    {
        var fields = namesAndValues.Chunk(2).Select(pair => new KeyValuePair<string, string>(pair[0], pair[1])).ToArray();

        var refusal = AzureConditions.WriteRefusal(fields, existing: null);

        Assert.AreEqual(expected, refusal?.Code ?? "none");
    }
}
