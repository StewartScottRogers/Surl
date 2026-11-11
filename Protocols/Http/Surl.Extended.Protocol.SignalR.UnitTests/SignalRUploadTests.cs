using System.Text;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// One <c>Collect</c> upload: its items kept byte for byte and counted against <c>--max-filesize</c>
/// (HubProtocol.md "Upload streaming"; ADR-0119, decisions 1 and 7).
/// </summary>
[TestClass]
public sealed class SignalRUploadTests
{
    [TestMethod]
    public void ItemsAsArray_ItemsAdded_AreOneArrayInOrderByteForByte()
    {
        var upload = new SignalRUpload("1", isStreaming: false);

        upload.TryAdd(Value("1"), 0);
        upload.TryAdd(Value("{ \"a\" : 2 }"), 0);

        Assert.AreEqual("[1,{ \"a\" : 2 }]", Text(upload.ItemsAsArray()));
    }

    [TestMethod]
    public void TryAdd_ItemBytesPastTheLimit_AbandonsTheUploadAndDropsItsItems()
    {
        var upload = new SignalRUpload("1", isStreaming: false);

        var first = upload.TryAdd(Value("12"), 3);
        var second = upload.TryAdd(Value("3"), 3);
        var third = upload.TryAdd(Value("4"), 3);

        Assert.IsTrue(first);
        Assert.IsTrue(second);
        Assert.IsFalse(third);
        Assert.IsTrue(upload.IsAbandoned);
        Assert.AreEqual("[]", Text(upload.ItemsAsArray()));
    }

    [TestMethod]
    public void TryAdd_StreamingUpload_KeepsNoItems()
    {
        var upload = new SignalRUpload("1", isStreaming: true);

        upload.TryAdd(Value("1"), 0);

        Assert.AreEqual("[]", Text(upload.ItemsAsArray()));
        Assert.IsTrue(upload.IsStreaming);
        Assert.AreEqual("1", upload.InvocationId);
    }

    private static HubJsonValue Value(string json) => new(Encoding.UTF8.GetBytes(json));

    private static string Text(HubJsonValue value) => Encoding.UTF8.GetString(value.Utf8Json.Span);
}
