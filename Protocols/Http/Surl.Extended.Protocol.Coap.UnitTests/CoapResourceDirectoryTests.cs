using System.Text;
using Surl.Content;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapResourceDirectory"/>: <c>/.well-known/core</c> in the CoRE link format, with its
/// query filter (RFC 6690 sections 3.1 and 4.1; ADR-0097 decision 1).
/// </summary>
[TestClass]
public sealed class CoapResourceDirectoryTests
{
    [TestMethod]
    public void Write_ListsVisibleFilesInOrdinalOrderOfPath_LeavingHiddenAndUnmappableOnesOut()
    {
        var store = new CoapTestStore(CoapTestStore.Listings)
            .WithDirectory("a")
            .WithFile("a/x.xml", "<x/>")
            .WithFile("a.cbor", "1")
            .WithFile(".hidden", "h")
            .WithFile("CON", "c")
            .WithFile("z", "zz");

        var links = Write(store);

        Assert.AreEqual("</a.cbor>;ct=60;sz=1;obs,</a/x.xml>;ct=41;sz=4;obs,</z>;ct=42;sz=2;obs", links);
    }

    [TestMethod]
    public void Write_EmptyStore_IsEmpty() => Assert.AreEqual(string.Empty, Write(new CoapTestStore(CoapTestStore.Listings)));

    [TestMethod]
    [DataRow("href=/a*", "</a.exi>;ct=47;sz=1;obs,</ab.txt>;ct=0;sz=2;obs")]
    [DataRow("href=/ab.txt", "</ab.txt>;ct=0;sz=2;obs")]
    [DataRow("ct=47", "</a.exi>;ct=47;sz=1;obs")]
    [DataRow("sz=2", "</ab.txt>;ct=0;sz=2;obs")]
    [DataRow("obs=", "</a.exi>;ct=47;sz=1;obs,</ab.txt>;ct=0;sz=2;obs")]
    [DataRow("rt=temperature", "")]
    public void Write_FirstQueryWithAValue_FiltersTheLinks(string filter, string expected)
    {
        var store = new CoapTestStore(CoapTestStore.Listings).WithFile("a.exi", "1").WithFile("ab.txt", "22");

        Assert.AreEqual(expected, Write(store, "novalue", filter, "ct=0"));
    }

    [TestMethod]
    public void Write_DirectoriesDeeperThanTheLimit_AreNotWalked()
    {
        var store = new CoapTestStore(CoapTestStore.Listings);
        var path = string.Empty;
        for (var depth = 0; depth <= CoapResourceDirectory.MaxDepth + 1; depth++)
        {
            path += "d/";
            store.WithDirectory(path);
            store.WithFile(path + "f", "1");
        }

        var links = Write(store).Split(',');

        Assert.HasCount(CoapResourceDirectory.MaxDepth, links);
    }

    [TestMethod]
    public void Write_MoreFilesThanTheLimit_KeepsTheFirstInOrder()
    {
        var store = new CoapTestStore(CoapTestStore.Listings);
        for (var index = 0; index <= CoapResourceDirectory.MaxLinks; index++)
        {
            store.WithFile($"f{index:D4}", string.Empty);
        }

        var links = Write(store).Split(',');

        Assert.HasCount(CoapResourceDirectory.MaxLinks, links);
        Assert.AreEqual("</f0999>;ct=42;sz=0;obs", links[^1]);
    }

    private static string Write(CoapTestStore store, params string[] queries) =>
        Encoding.UTF8.GetString(CoapResourceDirectory.Write(store.Store, queries, CancellationToken.None));
}
