using System.Text.Json;

namespace Surl.Cryptography.MlKem;

/// <summary>
/// ML-KEM test cases from NIST's ACVP server, embedded as Fixtures/Acvp/ML-KEM-768.json and
/// Fixtures/Acvp/ML-KEM-1024.json. They are copied unchanged, test case by test case, from the
/// internal projections at https://github.com/usnistgov/ACVP-Server/tree/master/gen-val/json-files
/// (ML-KEM-keyGen-FIPS203/internalProjection.json and ML-KEM-encapDecap-FIPS203/internalProjection.json),
/// keeping only the fields the tests read. Both downloaded 2026-11-06.
/// <list type="bullet">
/// <item>ML-KEM-768: keyGen group 2's cases 26 to 28; encapDecap group 2's encapsulation cases 26
/// to 28, group 5's decapsulation cases 86 and 88, group 9's decapsulation key checks 126 and 128,
/// and group 10's encapsulation key checks 136 and 137.</item>
/// <item>ML-KEM-1024: keyGen group 3's cases 51 to 53; encapDecap group 3's encapsulation cases 51
/// to 53, group 6's decapsulation cases 96 and 97, group 11's decapsulation key checks 146 and 148,
/// and group 12's encapsulation key checks 156 and 159.</item>
/// </list>
/// </summary>
internal static class UnitTestAcvpVectors
{
    private static readonly JsonDocument MlKem768Document = Load("ML-KEM-768.json");

    private static readonly JsonDocument MlKem1024Document = Load("ML-KEM-1024.json");

    /// <summary>
    /// The test cases in <paramref name="section"/> (keyGen, encapsulation, decapsulation,
    /// decapsulationKeyCheck or encapsulationKeyCheck) of <paramref name="parameterSet"/>,
    /// ML-KEM-768 or ML-KEM-1024.
    /// </summary>
    public static IEnumerable<JsonElement> Cases(string parameterSet, string section)
    {
        var document = parameterSet switch
        {
            "ML-KEM-768" => MlKem768Document,
            "ML-KEM-1024" => MlKem1024Document,
            _ => throw new ArgumentOutOfRangeException(nameof(parameterSet), parameterSet, "No ACVP fixture for this parameter set."),
        };
        return document.RootElement.GetProperty(section).EnumerateArray();
    }

    /// <summary>The hex field <paramref name="name"/> of <paramref name="testCase"/> as bytes.</summary>
    public static byte[] Bytes(this JsonElement testCase, string name) => Convert.FromHexString(testCase.GetProperty(name).GetString()!);

    private static JsonDocument Load(string fixture)
    {
        using var stream = typeof(UnitTestAcvpVectors).Assembly.GetManifestResourceStream(fixture)
            ?? throw new InvalidOperationException($"The ACVP fixture {fixture} is not embedded.");
        return JsonDocument.Parse(stream);
    }
}
