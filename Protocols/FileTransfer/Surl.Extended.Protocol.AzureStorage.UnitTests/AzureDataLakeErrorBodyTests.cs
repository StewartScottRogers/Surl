namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Data Lake endpoint's JSON error body, written from the <c>DataLakeStorageError</c>
/// definition the Data Lake pages carry (ADR-0109, decision 5.2).
/// </summary>
[TestClass]
public sealed class AzureDataLakeErrorBodyTests
{
    [TestMethod]
    public void Render_IsTheCodeAndTheMessageWithItsRequestIdAndTimeLines()
    {
        var body = AzureDataLakeErrorBody.Render(AzureError.PathNotFound, "5f0e8a3c-1d2b-4c6e-9f70-a1b2c3d4e5f6", AzureServerHarness.Now);

        Assert.AreEqual(
            "{\"error\":{\"code\":\"PathNotFound\",\"message\":\"The specified path does not exist.\\nRequestId:5f0e8a3c-1d2b-4c6e-9f70-a1b2c3d4e5f6\\nTime:2026-11-05T12:00:00.0000000Z\"}}",
            System.Text.Encoding.UTF8.GetString(body));
    }

    [TestMethod]
    public void Render_MessageWithAnApostrophe_IsNotEscaped()
    {
        var body = AzureDataLakeErrorBody.Render(AzureError.UnsupportedHttpVerb, "id", AzureServerHarness.Now);

        StringAssert.Contains(System.Text.Encoding.UTF8.GetString(body), "doesn't");
    }
}
