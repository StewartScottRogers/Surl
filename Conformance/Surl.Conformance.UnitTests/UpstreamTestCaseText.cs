using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Builds test case environments and turns Latin-1 text into bytes and back, for the parser's tests.
/// </summary>
internal static class UpstreamTestCaseText
{
    public static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    public static byte[] Bytes(string latin1Text) => Encoding.Latin1.GetBytes(latin1Text);

    public static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    public static UpstreamTestCaseEnvironment Environment(
        Dictionary<string, string>? variables = null,
        HashSet<string>? features = null,
        UnitTestInMemoryTestCaseFileReader? files = null) => new(
            variables ?? new Dictionary<string, string>(StringComparer.Ordinal),
            features ?? new HashSet<string>(StringComparer.Ordinal),
            files ?? new UnitTestInMemoryTestCaseFileReader(),
            new UnitTestFixedTimeProvider(Now));

    public static string Preprocess(string source, UpstreamTestCaseEnvironment? environment = null) =>
        Text(UpstreamTestCasePreprocessor.PreprocessToBytes(Bytes(source), environment ?? Environment()));
}
