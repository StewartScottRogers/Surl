namespace Surl.Cli;

/// <summary>What must be true before an <see cref="AiHelpExample"/> is run (ADR-0046 decision 6).</summary>
public enum AiHelpExamplePrecondition
{
    /// <summary>Nothing: the example runs as it is.</summary>
    None,

    /// <summary><c>&lt;path&gt;</c> is an existing directory no other surl holds.</summary>
    DataDirectoryExists,

    /// <summary>Another surl is serving <c>&lt;path&gt;</c> with <c>--directory</c>.</summary>
    DataDirectoryHeldByAnotherSurl,

    /// <summary>
    /// <c>http.keytab</c> is a keytab holding an AES key for an <c>HTTP</c> principal, and
    /// <c>users.txt</c> a <c>--user-file</c>.
    /// </summary>
    KeytabAndUserFileExist,
}
