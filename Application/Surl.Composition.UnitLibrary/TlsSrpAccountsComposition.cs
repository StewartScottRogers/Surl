using System.Security.Cryptography;
using System.Text;
using Surl.Cli;
using Surl.Cryptography.Srp;
using Surl.Protocol.Abstractions;
using Surl.TlsSrp;

namespace Surl.Composition;

/// <summary>
/// The TLS-SRP accounts the command line gives (ADR-0122 decision 4 and Amendment 1), read once at
/// start before any listener binds: the GnuTLS files <c>--srp-verifiers</c> and <c>--srp-groups</c>
/// name, then the one <c>--tlsuser</c> account, whose verifier is computed in RFC 5054's 2048-bit
/// group with a 16-byte salt drawn again until its first byte is not zero (OpenSSL's client hashes
/// the salt without leading zero bytes). The secret that keys unknown users' simulated salts is drawn
/// from the same source.
/// </summary>
internal static class TlsSrpAccountsComposition
{
    /// <summary>The length of the salt drawn for <c>--tlsuser</c>: 16 bytes.</summary>
    public const int SaltLength = 16;

    /// <summary>Composes the accounts.</summary>
    /// <param name="commandLine">The parsed command line, its TLS-SRP options already checked by the parser.</param>
    /// <param name="random">Draws the <c>--tlsuser</c> salt and the simulation secret.</param>
    /// <param name="readFile">Reads a file's bytes (the <c>ReadStartFile</c> seam); <see langword="null"/> reads the disk.</param>
    /// <returns>The accounts, and the <c>--srp-groups</c> indexes whose group is not RFC 5054 Appendix A's.</returns>
    /// <exception cref="TlsSrpAccountsException">A file cannot be read or is malformed, or a user is given twice.</exception>
    public static ComposedTlsSrpAccounts Compose(SurlCommandLine commandLine, RandomNumberGenerator random, Func<string, byte[]>? readFile)
    {
        var read = readFile ?? File.ReadAllBytes;
        var groups = ReadGroupFile(commandLine.SrpGroupFile, read);
        var entries = ReadVerifierFile(commandLine.SrpVerifierFile, groups, read);
        if (commandLine.TlsUser is { } userName)
        {
            entries.Add(ComputeEntry(userName, commandLine.TlsPassword!, random));
        }

        RefuseUserGivenTwice(entries);
        var simulationSecret = new byte[TlsSrpAccounts.SimulationSecretLength];
        random.GetBytes(simulationSecret);
        return new ComposedTlsSrpAccounts(new TlsSrpAccounts(entries, simulationSecret), IndexesOutsideRfc5054(groups));
    }

    // The parser gives --srp-groups and --srp-verifiers together; the groups file is read first,
    // since the verifiers name its indexes (ADR-0122 Amendment 2).
    private static IReadOnlyDictionary<string, SrpGroup> ReadGroupFile(string? groupFile, Func<string, byte[]> read) =>
        groupFile is null ? new Dictionary<string, SrpGroup>() : Parse("--srp-groups", groupFile, read, SrpGroupFile.Parse);

    private static List<SrpVerifierEntry> ReadVerifierFile(
        string? verifierFile, IReadOnlyDictionary<string, SrpGroup> groups, Func<string, byte[]> read) =>
        verifierFile is null ? [] : [.. Parse("--srp-verifiers", verifierFile, read, text => SrpVerifierFile.Parse(text, groups))];

    private static List<string> IndexesOutsideRfc5054(IReadOnlyDictionary<string, SrpGroup> groups) =>
        [.. groups.Where(group => !group.Value.IsRfc5054Group).Select(group => group.Key)];

    /// <summary>The <c>--tlsuser</c> account: a fresh salt and the verifier of the password's UTF-8 bytes.</summary>
    internal static SrpVerifierEntry ComputeEntry(string userName, string password, RandomNumberGenerator random)
    {
        var salt = new byte[SaltLength];
        do
        {
            random.GetBytes(salt);
        }
        while (salt[0] == 0);

        var group = SrpGroup.Rfc5054Group2048;
        var verifier = Srp6a.ComputeVerifier(group, salt, Encoding.UTF8.GetBytes(userName), Encoding.UTF8.GetBytes(password));
        return new SrpVerifierEntry(userName, salt, Srp6a.ToUnsignedBigEndian(verifier), group);
    }

    private static T Parse<T>(string optionName, string path, Func<string, byte[]> read, Func<string, T> parse)
    {
        string text;
        try
        {
            text = Encoding.UTF8.GetString(read(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Unreadable(optionName, path, exception.Message);
        }

        try
        {
            return parse(text);
        }
        catch (SrpFileFormatException exception)
        {
            throw Unreadable(optionName, path, exception.Message);
        }
    }

    private static TlsSrpAccountsException Unreadable(string optionName, string path, string reason) =>
        new(SurlExitCode.CouldNotReadFile, $"(37) Could not read the {optionName} file '{path}': {reason}");

    private static void RefuseUserGivenTwice(List<SrpVerifierEntry> entries)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (entries.FirstOrDefault(entry => !seen.Add(entry.UserName)) is { } repeated)
        {
            throw new TlsSrpAccountsException(SurlExitCode.FailedInit, $"(2) The TLS-SRP user '{repeated.UserName}' is given twice");
        }
    }
}
