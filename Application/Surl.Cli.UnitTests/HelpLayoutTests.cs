namespace Surl.Cli;

[TestClass]
public sealed class HelpLayoutTests
{
    // FormatOptionLines: curl's measured column rule.

    [TestMethod]
    public void FormatOptionLines_ShortLeftSides_PadToAtLeastFiveCharacters()
    {
        var lines = HelpLayout.FormatOptionLines([("-a", "x")]);

        CollectionAssert.AreEqual(new[] { " -a     x" }, lines.ToArray());
    }

    [TestMethod]
    public void FormatOptionLines_LongestLeftSide_SetsTheColumnForEveryLine()
    {
        var lines = HelpLayout.FormatOptionLines([("-a, --alpha", "First"), ("    --b", "Second")]);

        CollectionAssert.AreEqual(new[] { " -a, --alpha  First", "     --b      Second" }, lines.ToArray());
    }

    [TestMethod]
    public void FormatOptionLines_PageWiderThan79_PullsTheColumnLeftAndNeverCutsALeftSide()
    {
        var longLeftSide = new string('L', 50);
        var longDescription = new string('d', 40);

        var lines = HelpLayout.FormatOptionLines([(longLeftSide, longDescription), ("-b", "y")]);

        CollectionAssert.AreEqual(
            new[] { " " + longLeftSide + "  " + longDescription, " " + "-b".PadRight(39) + "  y" },
            lines.ToArray());
    }

    [TestMethod]
    public void FormatOptionLines_LineThatWouldReachColumn77_EndsItsDescriptionAtColumn79()
    {
        var description = new string('d', 50);

        var lines = HelpLayout.FormatOptionLines([(new string('L', 20), "short"), ("-b", description), (new string('M', 29), "x")]);

        Assert.AreEqual(" " + "-b".PadRight(26) + "  " + description, lines[1]);
        Assert.AreEqual(79, lines[1].Length);
    }

    [TestMethod]
    public void FormatOptionLines_DescriptionOf77OrMore_StartsTwoSpacesAfterTheLeftSide()
    {
        var description = new string('d', 80);

        var lines = HelpLayout.FormatOptionLines([("-c", description), ("-e", "short")]);

        CollectionAssert.AreEqual(new[] { " -c  " + description, " -e  short" }, lines.ToArray());
    }

    // WrapNameList: the pointer lines of the short list.

    [TestMethod]
    public void WrapNameList_JoinsWithCommasEndsWithAFullStopAndDropsTrailingSpaces()
    {
        var lines = HelpLayout.WrapNameList([new string('n', 71), "abcd", "z"]);

        CollectionAssert.AreEqual(new[] { new string('n', 71) + ", abcd,", "z." }, lines.ToArray());
    }

    [TestMethod]
    public void WrapNameList_CountsTheSpaceAfterEachComma()
    {
        var lines = HelpLayout.WrapNameList([new string('n', 71), "abcde", "z"]);

        CollectionAssert.AreEqual(new[] { new string('n', 71) + ",", "abcde, z." }, lines.ToArray());
    }

    // WrapParagraph: the paragraphs of an option page.

    [TestMethod]
    public void WrapParagraph_ShortParagraph_IsOneIndentedLine()
    {
        var lines = HelpLayout.WrapParagraph("Get help for commands.");

        CollectionAssert.AreEqual(new[] { "        Get help for commands." }, lines.ToArray());
    }

    [TestMethod]
    public void WrapParagraph_LongParagraph_WrapsGreedilyWithin79Columns()
    {
        var paragraph = string.Join(' ', Enumerable.Repeat("abcdefghi", 10));

        var lines = HelpLayout.WrapParagraph(paragraph);

        CollectionAssert.AreEqual(
            new[]
            {
                "        " + string.Join(' ', Enumerable.Repeat("abcdefghi", 7)),
                "        " + string.Join(' ', Enumerable.Repeat("abcdefghi", 3)),
            },
            lines.ToArray());
    }

    [TestMethod]
    public void WrapParagraph_WordEndingInColumn79_StaysOnItsLine()
    {
        var word = new string('w', 71);

        var lines = HelpLayout.WrapParagraph(word + " next");

        CollectionAssert.AreEqual(new[] { "        " + word, "        next" }, lines.ToArray());
    }

    [TestMethod]
    public void WrapParagraph_WordTooLongForAnyLine_IsPutAloneOnOne()
    {
        var word = new string('w', 80);

        var lines = HelpLayout.WrapParagraph("a " + word + " b");

        CollectionAssert.AreEqual(new[] { "        a", "        " + word, "        b" }, lines.ToArray());
    }
}
