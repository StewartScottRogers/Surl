using System.Text.Json;

namespace Surl.Cryptography.MlKem;

/// <summary>
/// ML-KEM-768 test cases from NIST's ACVP server, embedded as Fixtures/Acvp/ML-KEM-768.json. They
/// are copied unchanged, test case by test case, from the internal projections at
/// https://github.com/usnistgov/ACVP-Server/tree/master/gen-val/json-files: ML-KEM-keyGen-FIPS203
/// (test group 2, test cases 26 to 28), and ML-KEM-encapDecap-FIPS203 (group 2's encapsulation
/// cases 26 to 28, group 5's decapsulation cases 86 and 88, group 9's decapsulation key checks
/// 126 and 128, and group 10's encapsulation key checks 136 and 137). Downloaded 2026-11-06.
/// </summary>
internal static class UnitTestAcvpVectors
{
    private static readonly JsonDocument Document = Load();

    /// <summary>The test cases in <paramref name="section"/>: keyGen, encapsulation, decapsulation, decapsulationKeyCheck or encapsulationKeyCheck.</summary>
    public static IEnumerable<JsonElement> Cases(string section) => Document.RootElement.GetProperty(section).EnumerateArray();

    /// <summary>The hex field <paramref name="name"/> of <paramref name="testCase"/> as bytes.</summary>
    public static byte[] Bytes(this JsonElement testCase, string name) => Convert.FromHexString(testCase.GetProperty(name).GetString()!);

    private static JsonDocument Load()
    {
        using var stream = typeof(UnitTestAcvpVectors).Assembly.GetManifestResourceStream("ML-KEM-768.json")
            ?? throw new InvalidOperationException("The ACVP fixture ML-KEM-768.json is not embedded.");
        return JsonDocument.Parse(stream);
    }
}
