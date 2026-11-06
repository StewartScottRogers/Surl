namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseStripLineTests
{
    [TestMethod]
    [DataRow(@"s/^(EPRT \|1\|)(\S*)/$1/", "EPRT |1|127.0.0.1|4000|\n", "EPRT |1|\n")]
    [DataRow(@"s/^(229 Entering Passive Mode \().*(\).*)/${1}stripped${2}/", "229 Entering Passive Mode (|||4000|)\r\n", "229 Entering Passive Mode (stripped)\r\n")]
    [DataRow(@"s/(AES[-_])\d\d\d([-_]CBC)/$1NNN$2/", "AES-256-CBC\n", "AES-NNN-CBC\n")]
    [DataRow(@"s/^EPRT \|1\|(\S*)/EPRT \|1\|/", "EPRT |1|x\n", "EPRT |1|\n")]
    [DataRow(@"s:\\:/:g", "a\\b\\c\n", "a/b/c\n")]
    [DataRow(@"s:^(MEM )(.*[/\\])(.*):$1$3:", "MEM lib/url.c: x\n", "MEM url.c: x\n")]
    [DataRow(@"s/a/b/", "aaa\n", "baa\n")]
    [DataRow(@"s/a/b/g", "aaa\n", "bbb\n")]
    [DataRow(@"s/A/b/gi", "aAa\n", "bbb\n")]
    [DataRow(@"s/x/\r\n\t\\\//", "x", "\r\n\t\\/")]
    [DataRow(@"s/(x)/$9-${1}/", "x", "-x")]
    [DataRow(@"s/x/a@ b@/", "x", "a@ b@")]
    [DataRow(@"s/com\x00\x00(\x1c|\x01)/com-00-00!/g;", "com\0\0\u001c.", "com-00-00!.")]
    [DataRow(@"s/^\n//", "\n", "")]
    [DataRow(@"s/^.*(?=\* Ignoring Content-Length )// or $_ = ''", "x * Ignoring Content-Length \n", "* Ignoring Content-Length \n")]
    [DataRow(@"s/^.*(?=\* Ignoring Content-Length )// or $_ = ''", "other\n", "")]
    [DataRow(@"$_ = '' if /CURLOPT_USERAGENT/", "CURLOPT_USERAGENT x\n", "")]
    [DataRow(@"$_ = '' if /CURLOPT_USERAGENT/", "CURLOPT_URL x\n", "CURLOPT_URL x\n")]
    [DataRow(@"$_ = '' if(($_ !~ /left intact/) and ($_ !~ /Closing connection/))", "left intact\n", "left intact\n")]
    [DataRow(@"$_ = '' if(($_ !~ /left intact/) and ($_ !~ /Closing connection/))", "other\n", "")]
    [DataRow(@"$_ = '' if((($_ !~ /tool_paramhlp/) and ($_ !~ /tool_cfgable/)) || ($_ =~ /free\(\(nil\)\)/))", "tool_cfgable free((nil))\n", "")]
    [DataRow(@"$_ = '' if((($_ !~ /tool_paramhlp/) and ($_ !~ /tool_cfgable/)) || ($_ =~ /free\(\(nil\)\)/))", "tool_cfgable\n", "tool_cfgable\n")]
    [DataRow(@"$_ = '' if /a/ && not /b/;", "a\n", "")]
    [DataRow(@"$_ = '' if /a/ && ! /b/", "ab\n", "ab\n")]
    [DataRow(@"$_ = '' if /a/ or /b/", "b\n", "")]
    [DataRow(@"$_ = '' if /a/ or /b/", "c\n", "c\n")]
    [DataRow(@"$_ = '' if /a/ || /b/", "a\n", "")]
    [DataRow("# a comment", "kept\n", "kept\n")]
    [DataRow("   ", "kept\n", "kept\n")]
    public void Apply_SupportedLine_LeavesTheLineAsPerlDoes(string strip, string line, string expected)
    {
        var operation = UpstreamTestCaseStripLine.TryParse(strip);

        Assert.IsNotNull(operation);
        Assert.AreEqual(expected, operation.Apply(line));
    }

    [TestMethod]
    [DataRow(@"if(s/^(Authorization: Digest )([^\r\n]+)(\r?\n)$//) { $_ = $1 . join(', ', map { $_ } sort split(/, */, $2)) . $3; }")]
    [DataRow(@"s/a/b/x")]
    [DataRow(@"s{a}{b}")]
    [DataRow(@"s/[\S]/b/")]
    [DataRow(@"s/a/\q/")]
    [DataRow(@"s/a/$&/")]
    [DataRow(@"s/a/@foo/")]
    [DataRow(@"s/a/@_/")]
    [DataRow(@"s/a/x\/")]
    [DataRow(@"chomp;")]
    [DataRow(@"$_ = '' if")]
    [DataRow(@"$_ = '' if /a/ or")]
    [DataRow(@"$_ = '' if /a/ and")]
    [DataRow(@"$_ = '' if not")]
    [DataRow(@"$_ = '' if (/a/")]
    [DataRow(@"$_ = '' if ()")]
    [DataRow(@"$_ = '' if $_ /a/")]
    [DataRow(@"$_ = '' if $_ eq /a/")]
    [DataRow(@"$_ = '' if /a")]
    [DataRow(@"$_ = '' if /(a/")]
    [DataRow(@"$_ = '' if /a/ /b/")]
    [DataRow(@"$_ = '' if /a/ xor /b/")]
    [DataRow(@"$_ = '' if # x")]
    public void TryParse_LineOutsideTheSubset_IsNull(string strip) =>
        Assert.IsNull(UpstreamTestCaseStripLine.TryParse(strip));

    [TestMethod]
    [DataRow("strip", "^User-Agent:", true)]
    [DataRow("strip", "(unclosed", false)]
    [DataRow("strippart", "s/a/b/", true)]
    [DataRow("stripfile1", "chomp;", false)]
    public void IsSupported_PartAndLine_IsWhetherTheCheckerInterpretsIt(string part, string line, bool supported) =>
        Assert.AreEqual(supported, UpstreamTestCaseStripLine.IsSupported(part, line));

    [TestMethod]
    public void NullArguments_Throw()
    {
        var operation = UpstreamTestCaseStripLine.TryParse("s/a/b/")!;

        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseStripLine.TryParse(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseStripLine.IsSupported(null!, "x"));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseStripLine.IsSupported("strip", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => operation.Apply(null!));
    }
}
