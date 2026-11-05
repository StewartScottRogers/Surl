using static Surl.Conformance.UpstreamTestCaseText;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCasePreprocessorTests
{
    [TestMethod]
    public void Preprocess_Base64_EncodesVariablesAndPercentEscapes()
    {
        var environment = Environment(Variables(("HTTPPORT", "8990")));

        var result = Preprocess("%b64[%HTTPPORT %9a]b64%\n", environment);

        Assert.AreEqual("ODk5MCCa\n", result);
    }

    [TestMethod]
    public void Preprocess_Base64_IsCaseInsensitiveAndExpandsEachOccurrence()
    {
        var result = Preprocess("%B64[a]B64% %b64[b]b64%\n");

        Assert.AreEqual("YQ== Yg==\n", result);
    }

    [TestMethod]
    public void Preprocess_Hex_DecodesEscapesAndKeepsOtherBytes()
    {
        var result = Preprocess("%hex[%00%01%FFab]hex%\n");

        Assert.AreEqual("\0\u0001ÿab\n", result);
    }

    [TestMethod]
    public void Preprocess_Repeat_RepeatsTheTextWithItsEscapesDecoded()
    {
        var result = Preprocess("%repeat[3 x a%41]%\n");

        Assert.AreEqual("aAaAaA\n", result);
    }

    [TestMethod]
    public void Preprocess_RepeatCountTooLarge_ReportsTheLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(
            () => Preprocess("first\n%repeat[99999999999 x a]%\n"));

        Assert.AreEqual(2, exception.LineNumber);
    }

    [TestMethod]
    public void Preprocess_Days_GivesTheMinuteAlignedAlternatives()
    {
        var result = Preprocess("%days[2]\n");

        Assert.AreEqual("%alternatives[1700172780,1700172840]\n", result);
    }

    [TestMethod]
    public void Preprocess_DaysNotANumber_CountsAsNoDays()
    {
        var result = Preprocess("%days[soon]\n");

        Assert.AreEqual("%alternatives[1699999980,1700000040]\n", result);
    }

    [TestMethod]
    public void Preprocess_WhitespaceMacros_BecomeTheirCharacters()
    {
        var result = Preprocess("a%SP%TABb%CR\n%SP\n");

        Assert.AreEqual("a \tb\r\n \n", result);
    }

    [TestMethod]
    public void Preprocess_SpecialCharacterMacros_BecomeTheirCharacters()
    {
        var result = Preprocess("%LTa%GT %AMP\n");

        Assert.AreEqual("<a> &\n", result);
    }

    [TestMethod]
    public void Preprocess_Include_InsertsTheFileVerbatimInPlaceOfTheLine()
    {
        var files = new UnitTestInMemoryTestCaseFileReader().With("log/inc", "one\r\ntwo\n");
        var environment = Environment(Variables(("LOGDIR", "log")), files: files);

        var result = Preprocess("before\n%include %LOGDIR/inc%\nafter\n", environment);

        Assert.AreEqual("before\none\r\ntwo\nafter\n", result);
    }

    [TestMethod]
    public void Preprocess_IncludeText_MakesCrlfLfAndExpandsWhatItIncluded()
    {
        var files = new UnitTestInMemoryTestCaseFileReader().With("inc", "port %HTTPPORT%SP\r\n");
        var environment = Environment(Variables(("HTTPPORT", "8990")), files: files);

        var result = Preprocess("%includetext inc%\nafter\n", environment);

        Assert.AreEqual("port 8990 \nafter\n", result);
    }

    [TestMethod]
    public void Preprocess_IncludedFileMissing_IncludesNothingAsUpstreamDoes()
    {
        var result = Preprocess("a\n%include nowhere%\n%includetext nowhere%\nb\n");

        Assert.AreEqual("a\nb\n", result);
    }

    [TestMethod]
    public void Preprocess_DigestedFileMissing_ReportsTheLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(
            () => Preprocess("a\nb\n%sha256b64file[nowhere]sha256b64file%\n"));

        Assert.AreEqual(3, exception.LineNumber);
        Assert.IsInstanceOfType<FileNotFoundException>(exception.InnerException);
    }

    [TestMethod]
    public void Preprocess_DigestedFilePathRefused_ReportsTheLine()
    {
        var files = new UnitTestInMemoryTestCaseFileReader().Refusing("bad");
        var environment = Environment(files: files);

        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(
            () => Preprocess("%strippemfile[bad]strippemfile%\n", environment));

        Assert.IsInstanceOfType<ArgumentException>(exception.InnerException);
    }

    [TestMethod]
    public void Preprocess_IncludedFilePathRefused_IncludesNothing()
    {
        var files = new UnitTestInMemoryTestCaseFileReader().Refusing("bad");
        var environment = Environment(files: files);

        var result = Preprocess("%include bad%\nb\n", environment);

        Assert.AreEqual("b\n", result);
    }

    [TestMethod]
    public void Preprocess_RepeatOfEmptyText_IsEmpty()
    {
        var result = Preprocess("<%repeat[99999999999 x ]%>\n");

        Assert.AreEqual("<>\n", result);
    }

    [TestMethod]
    public void Preprocess_RepeatOverTheMaximumLength_ReportsTheLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(
            () => Preprocess("%repeat[67108865 x a]%\n"));

        Assert.AreEqual(1, exception.LineNumber);
    }

    [TestMethod]
    public void Preprocess_DaysWithAFraction_CountsTheFraction()
    {
        var result = Preprocess("%days[0.5]\n");

        Assert.AreEqual("%alternatives[1700043180,1700043240]\n", result);
    }

    [TestMethod]
    public void Preprocess_IncludeWithoutFinalNewline_JoinsTheNextLine()
    {
        var files = new UnitTestInMemoryTestCaseFileReader().With("inc", "abc");
        var environment = Environment(files: files);

        var lines = UpstreamTestCasePreprocessor.Preprocess(Bytes("%include inc%\nnext\nlast"), environment);

        CollectionAssert.AreEqual(new[] { "abcnext\n", "last" }, lines.Select(line => line.Text).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 3 }, lines.Select(line => line.SourceLineNumber).ToArray());
    }

    [TestMethod]
    public void Preprocess_IncludedLines_EachNameTheIncludingLine()
    {
        var files = new UnitTestInMemoryTestCaseFileReader().With("inc", "x\ny\n");
        var environment = Environment(files: files);

        var lines = UpstreamTestCasePreprocessor.Preprocess(Bytes("a\n%include inc%\nb\n"), environment);

        CollectionAssert.AreEqual(new[] { 1, 2, 2, 3 }, lines.Select(line => line.SourceLineNumber).ToArray());
    }

    [TestMethod]
    public void Preprocess_Sha256FileAndStripPemFile_ReadTheNamedFile()
    {
        var files = new UnitTestInMemoryTestCaseFileReader()
            .With("a b", "abc")
            .With("cert", "junk\r\n-----BEGIN X-----\r\nAAA\r\n-----END X-----\r\ntrailer\r\n");
        var environment = Environment(files: files);

        var result = Preprocess("%sha256b64file[a%20b]sha256b64file% %strippemfile[cert]strippemfile%\n", environment);

        // The pattern runs before CRLF becomes LF, so "-----END X-----[\r\n]?" keeps the CR, the
        // CR becomes the final LF, and that is the newline chomp removes.
        Assert.AreEqual("ungWv48Bz+pBQUDeXa4iI7ADYaOWF3qctBD/YfIAFa0= -----BEGIN X-----\nAAA\n-----END X-----\n", result);
    }

    [TestMethod]
    public void Preprocess_StripPemFileWithoutFinalNewline_KeepsItsLastCharacter()
    {
        var files = new UnitTestInMemoryTestCaseFileReader().With("cert", "-----BEGIN X-----A-----END X-----");
        var environment = Environment(files: files);

        var result = Preprocess("%strippemfile[cert]strippemfile%\n", environment);

        Assert.AreEqual("-----BEGIN X-----A-----END X-----\n", result);
    }

    [TestMethod]
    public void Preprocess_SourceCrlf_IsReadAsLf()
    {
        var result = Preprocess("a\r\nb\rc\r\n");

        Assert.AreEqual("a\nb\rc\n", result);
    }

    [TestMethod]
    public void Preprocess_Variables_SubstitutesGivenAndLeavesUnknownAsWritten()
    {
        var environment = Environment(Variables(("HOSTIP", "127.0.0.1"), ("CLIENT6IP", "[::1]"), ("CLIENT6IP-NB", "::1")));

        var result = Preprocess("%HOSTIP %CLIENT6IP-NB %CLIENT6IP %HTTPPORT %NOSUCHVAR\n", environment);

        Assert.AreEqual("127.0.0.1 ::1 [::1] %HTTPPORT %NOSUCHVAR\n", result);
    }

    [TestMethod]
    public void Preprocess_VariableNameUpstreamDoesNotKnow_IsRefused()
    {
        var environment = Environment(Variables(("NOSUCHVAR", "x")));

        Assert.ThrowsExactly<ArgumentException>(() => Preprocess("a\n", environment));
    }

    [TestMethod]
    public void Preprocess_IfFeaturePresent_KeepsTheLines()
    {
        var environment = Environment(features: Features("brotli"));

        var result = Preprocess("%if brotli\nyes\n%else\nno\n%endif\n", environment);

        Assert.AreEqual("yes\n", result);
    }

    [TestMethod]
    public void Preprocess_IfFeatureAbsent_KeepsTheElseLines()
    {
        var result = Preprocess("%if brotli\nyes\n%else\nno\n%endif\nend\n");

        Assert.AreEqual("no\nend\n", result);
    }

    [TestMethod]
    public void Preprocess_IfNotFeature_InvertsTheCondition()
    {
        var result = Preprocess("%if !brotli\nnot-brotli\n%endif\n");

        Assert.AreEqual("not-brotli\n", result);
    }

    [TestMethod]
    public void Preprocess_NestedConditions_HideTheInnerElseUnderAHiddenOuter()
    {
        var environment = Environment(features: Features("b"));

        var result = Preprocess("%if a\n%if b\n1\n%else\n2\n%endif\n%else\n%if b\n3\n%else\n4\n%endif\n%endif\n", environment);

        Assert.AreEqual("3\n", result);
    }

    [TestMethod]
    public void Preprocess_ElseWithoutIf_ReportsTheLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(() => Preprocess("a\n%else\n"));

        Assert.AreEqual(2, exception.LineNumber);
        Assert.AreEqual("%else with no %if", exception.Problem);
    }

    [TestMethod]
    public void Preprocess_EndifWithoutIf_ReportsTheLine()
    {
        var exception = Assert.ThrowsExactly<UpstreamTestCaseFormatException>(() => Preprocess("  %endif\n"));

        Assert.AreEqual(1, exception.LineNumber);
        Assert.AreEqual("%endif with no %if", exception.Problem);
    }

    [TestMethod]
    public void Preprocess_DataCrlfYes_MakesEveryDataLineCrlfAndNothingOutside()
    {
        var result = Preprocess("<reply>\n<data crlf=\"yes\">\nHTTP/1.1 200 OK\nbody\n</data>\n</reply>\n");

        Assert.AreEqual("<reply>\n<data crlf=\"yes\">\r\nHTTP/1.1 200 OK\r\nbody\r\n</data>\n</reply>\n", result);
    }

    [TestMethod]
    public void Preprocess_DataCrlfHeaders_ConvertsOnlyTheHeaderPart()
    {
        var result = Preprocess(
            "<data crlf=\"headers\">\nHTTP/1.1 200 OK\nContent-Length: 6\n\nbody text\n\nmore\n</data>\n");

        Assert.AreEqual(
            "<data crlf=\"headers\">\nHTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\nbody text\n\nmore\n</data>\n",
            result);
    }

    [TestMethod]
    public void Preprocess_ConnectCrlf_IsConvertedAndEndsAtItsClosingTag()
    {
        var result = Preprocess("<connect crlf=\"yes\">\na\n</connect>\n<data2>\nb\n</data2>\n");

        Assert.AreEqual("<connect crlf=\"yes\">\r\na\r\n</connect>\n<data2>\nb\n</data2>\n", result);
    }

    [TestMethod]
    public void Preprocess_DataWithoutCrlf_LeavesNewlines()
    {
        var result = Preprocess("<data nocheck=\"yes\">\na\n</data>\n");

        Assert.AreEqual("<data nocheck=\"yes\">\na\n</data>\n", result);
    }

    [TestMethod]
    public void SplitLines_TextWithoutFinalNewline_KeepsTheLastLine()
    {
        var lines = UpstreamTestCasePreprocessor.SplitLines("a\n\nb");

        CollectionAssert.AreEqual(new[] { "a\n", "\n", "b" }, lines);
    }

    private static Dictionary<string, string> Variables(params (string Name, string Value)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

    private static HashSet<string> Features(params string[] names) => new(names, StringComparer.Ordinal);
}
