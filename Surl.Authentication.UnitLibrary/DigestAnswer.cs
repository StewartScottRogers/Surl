using System.Globalization;

namespace Surl.Authentication;

/// <summary>
/// The parameters of an <c>Authorization: Digest</c> answer Surl can check (RFC 7616 section
/// 3.4, ADR-0036): every one <c>qop=auth</c> needs is present, the realm is
/// <see cref="DigestAuthenticationMethod.Realm"/>, <c>qop</c> is <c>auth</c>, <c>nc</c> is
/// eight hex digits, the algorithm is one of the six Surl knows, and <c>userhash</c>, never
/// offered, is absent or <c>false</c>.
/// </summary>
internal sealed record DigestAnswer(
    string UserName,
    DigestAlgorithmName Algorithm,
    string Response,
    DigestResponseInputs Inputs)
{
    /// <summary>
    /// Reads the text after <c>Digest</c> in an <c>Authorization</c> field.
    /// </summary>
    /// <param name="credentials">The parameters, as received.</param>
    /// <param name="method">The request method, which the response covers.</param>
    /// <returns>The answer, or <see langword="null"/> when it is malformed or not one Surl can check.</returns>
    public static DigestAnswer? TryRead(string credentials, string method)
    {
        var parameters = DigestParameterParser.Parse(credentials);

        return parameters is null ? null : TryRead(parameters, method);
    }

    /// <summary>
    /// The <c>nc</c> parameter as a number.
    /// </summary>
    public uint NonceCount => uint.Parse(Inputs.NonceCount, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);

    private static DigestAnswer? TryRead(Dictionary<string, string> parameters, string method)
    {
        var algorithm = DigestAlgorithmName.Parse(parameters.GetValueOrDefault("algorithm"));
        string?[] required = [.. new[] { "username", "realm", "nonce", "uri", "response", "cnonce", "nc", "qop" }
            .Select(parameters.GetValueOrDefault)];
        if (algorithm is null || required.Contains(null) || !IsCheckable(parameters))
        {
            return null;
        }

        return new DigestAnswer(
            parameters["username"],
            algorithm,
            parameters["response"],
            new DigestResponseInputs(
                method, parameters["uri"], parameters["nonce"], parameters["nc"], parameters["cnonce"], parameters["qop"]));
    }

    private static bool IsCheckable(Dictionary<string, string> parameters) =>
        parameters["realm"] == DigestAuthenticationMethod.Realm
        && parameters["qop"] == "auth"
        && parameters["nc"].Length == 8
        && uint.TryParse(parameters["nc"], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _)
        && string.Equals(parameters.GetValueOrDefault("userhash", "false"), "false", StringComparison.OrdinalIgnoreCase);
}
