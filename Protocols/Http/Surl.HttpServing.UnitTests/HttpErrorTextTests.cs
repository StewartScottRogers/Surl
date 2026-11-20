using System.Text;
using Surl.HttpMessage;

namespace Surl.HttpServing;

[TestClass]
public sealed class HttpErrorTextTests
{
    [TestMethod]
    [DataRow(399, false)]
    [DataRow(400, true)]
    [DataRow(404, true)]
    [DataRow(503, true)]
    [DataRow(304, false)]
    [DataRow(204, false)]
    public void Carries_Status_IsTrueFrom400Up(int code, bool expected) =>
        Assert.AreEqual(expected, HttpErrorText.Carries(new HttpStatus(code, "x")));

    [TestMethod]
    public void BytesOf_StatusWithReason_IsCodeSpaceReasonAndALineFeed() =>
        Assert.AreEqual("404 Not Found\n", Encoding.ASCII.GetString(HttpErrorText.BytesOf(HttpStatus.NotFound)));

    [TestMethod]
    public void BytesOf_StatusWithNoReason_IsTheCodeAndALineFeed() =>
        Assert.AreEqual("599\n", Encoding.ASCII.GetString(HttpErrorText.BytesOf(new HttpStatus(599, string.Empty))));

    [TestMethod]
    public void BodyOf_GetAnswer_HoldsTheText()
    {
        var body = HttpErrorText.BodyOf(HttpStatus.NotFound, isHead: false);

        Assert.AreEqual("404 Not Found\n", Encoding.ASCII.GetString(((HttpAnswerBody.Bytes)body).Content.Span));
    }

    [TestMethod]
    public void BodyOf_HeadAnswer_IsNone() =>
        Assert.AreSame(HttpAnswerBody.None, HttpErrorText.BodyOf(HttpStatus.NotFound, isHead: true));

    [TestMethod]
    public void FieldsOf_Status_NameTheTextTypeAndItsLength()
    {
        var fields = HttpErrorText.FieldsOf(HttpStatus.NotFound);

        CollectionAssert.AreEqual(
            new[] { new KeyValuePair<string, string>("Content-Type", "text/plain; charset=utf-8"), new("Content-Length", "14") },
            fields.ToArray());
    }
}
